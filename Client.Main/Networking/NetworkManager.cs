using Client.Main.Client;
using Client.Main.Configuration;
using Client.Main.Core.Models;
using Client.Main.Networking.PacketHandling;
using Client.Main.Networking.Services;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets; // For CharacterClassNumber
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.SimpleModulus;
using MUnique.OpenMU.Network.Xor;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Networking
{
    public sealed class NetworkManager : IAsyncDisposable
    {
        // Static constants from SimpleLoginClient
        private static readonly SimpleModulusKeys EncryptKeys = PipelinedSimpleModulusEncryptor.DefaultClientKey;
        private static readonly SimpleModulusKeys DecryptKeys = PipelinedSimpleModulusDecryptor.DefaultClientKey;
        private static readonly byte[] Xor3Keys = DefaultKeys.Xor3Keys;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<NetworkManager> _logger;
        private readonly MuOnlineSettings _settings;
        private readonly ConnectionManager _connectionManager;
        private readonly PacketRouter _packetRouter;
        private readonly LoginService _loginService;
        private readonly CharacterService _characterService;
        private readonly ConnectServerService _connectServerService;
        private readonly CharacterState _characterState; // Keep track of character state
        private readonly ScopeManager _scopeManager;     // Keep track of objects in scope
        private readonly Dictionary<byte, byte> _serverDirectionMap;

        private ClientConnectionState _currentState = ClientConnectionState.Initial;
        private List<ServerInfo> _serverList = new();
        private CancellationTokenSource? _managerCts; // For overall manager cancellation

        // --- Events for UI Communication ---
        public event EventHandler<ClientConnectionState>? ConnectionStateChanged;
        public event EventHandler<List<ServerInfo>>? ServerListReceived;
        public event EventHandler<string>? ErrorOccurred;
        // *** CHANGE EVENT SIGNATURE HERE ***
        public event EventHandler<List<(string Name, CharacterClassNumber Class, ushort Level)>>? CharacterListReceived;
        public event EventHandler? LoginSuccess;
        public event EventHandler? LoginFailed;
        public event EventHandler? EnteredGame;
        // Add more events as needed (e.g., ChatMessageReceived, CharacterStatsUpdated)

        // --- Public Properties ---
        public ClientConnectionState CurrentState => _currentState;
        public bool IsConnected => _connectionManager.IsConnected;
        public CharacterState GetCharacterState() => _characterState;

        public NetworkManager(ILoggerFactory loggerFactory, MuOnlineSettings settings, CharacterState characterState, ScopeManager scopeManager)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<NetworkManager>();
            _settings = settings;
            _characterState = characterState; // Store the character state
            _scopeManager = scopeManager;     // Store the scope manager

            var clientVersionBytes = Encoding.ASCII.GetBytes(settings.ClientVersion);
            var clientSerialBytes = Encoding.ASCII.GetBytes(settings.ClientSerial);
            var targetVersion = System.Enum.Parse<TargetProtocolVersion>(settings.ProtocolVersion, ignoreCase: true);

            _connectionManager = new ConnectionManager(loggerFactory, EncryptKeys, DecryptKeys);

            // Initialize services first
            _loginService = new LoginService(_connectionManager, _loggerFactory.CreateLogger<LoginService>(), clientVersionBytes, clientSerialBytes, Xor3Keys);
            _characterService = new CharacterService(_connectionManager, _loggerFactory.CreateLogger<CharacterService>());
            _connectServerService = new ConnectServerService(_connectionManager, _loggerFactory.CreateLogger<ConnectServerService>());

            // Initialize PacketRouter, passing necessary dependencies
            // NOTE: Pass 'this' NetworkManager instance to PacketRouter and handlers if they need to raise events or access manager state
            _packetRouter = new PacketRouter(loggerFactory, _characterService, _loginService, targetVersion, this, _characterState, _scopeManager, _settings); // Pass 'this'

            _managerCts = new CancellationTokenSource();

            _serverDirectionMap = settings.DirectionMap?.ToDictionary(kv => Convert.ToByte(kv.Key), kv => kv.Value)
                      ?? new Dictionary<byte, byte>();

        }

        public IReadOnlyDictionary<byte, byte> GetDirectionMap()
        {
            return _serverDirectionMap;
        }

        // --- Public Methods for UI Interaction ---

        // **** DODAJ TĘ METODĘ ****
        /// <summary>
        /// Gets a read-only view of the currently cached server list.
        /// </summary>
        /// <returns>A read-only list of ServerInfo objects.</returns>
        public IReadOnlyList<ServerInfo> GetCachedServerList()
        {
            // Zwracamy kopię lub widok ReadOnly, aby zapobiec modyfikacji z zewnątrz
            // ToList() tworzy nową listę (kopię).
            // return _serverList.ToList();
            // Lub użyj ReadOnlyCollection dla wydajniejszego widoku tylko do odczytu:
            return new ReadOnlyCollection<ServerInfo>(_serverList);
        }
        // **** KONIEC DODANEJ METODY ****

        public Task SendWalkRequestAsync(byte startX, byte startY, byte[] path)
            => _characterService.SendWalkRequestAsync(startX, startY, path);

        public Task SendInstantMoveRequestAsync(byte x, byte y)
            => _characterService.SendInstantMoveRequestAsync(x, y);

        public async Task ConnectToConnectServerAsync()
        {
            if (_connectionManager.IsConnected)
            {
                OnErrorOccurred("Already connected. Disconnect first.");
                return;
            }

            var cancellationToken = _managerCts?.Token ?? CancellationToken.None;
            if (cancellationToken.IsCancellationRequested) return;

            UpdateState(ClientConnectionState.ConnectingToConnectServer);
            _packetRouter.SetRoutingMode(true); // Set routing to CS mode

            if (await _connectionManager.ConnectAsync(_settings.ConnectServerHost, _settings.ConnectServerPort, false, cancellationToken))
            {
                var csConnection = _connectionManager.Connection;
                csConnection.PacketReceived += HandlePacketAsync;
                csConnection.Disconnected += HandleDisconnectAsync;
                _connectionManager.StartReceiving(cancellationToken); // Start listening AFTER setup

                // State is updated when Hello packet is received
            }
            else
            {
                OnErrorOccurred($"Connection to Connect Server {_settings.ConnectServerHost}:{_settings.ConnectServerPort} failed.");
                UpdateState(ClientConnectionState.Disconnected);
            }
        }

        public async Task RequestServerListAsync()
        {
            if (_currentState != ClientConnectionState.ConnectedToConnectServer && _currentState != ClientConnectionState.ReceivedServerList)
            {
                OnErrorOccurred($"Cannot request server list in state: {_currentState}");
                return;
            }
            UpdateState(ClientConnectionState.RequestingServerList);
            await _connectServerService.RequestServerListAsync();
        }

        public async Task RequestGameServerConnectionAsync(ushort serverId)
        {
            // **** ZMIENIONY WARUNEK: Sprawdź stany "w trakcie" lub "połączony" ****
            if (_currentState >= ClientConnectionState.RequestingConnectionInfo && _currentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("RequestGameServerConnectionAsync called while already connecting/connected to GS or requesting info. Ignoring request for ServerId: {ServerId}. CurrentState: {State}", serverId, _currentState);
                return; // Już w trakcie lub zakończono ten etap, zignoruj
            }
            // **** KONIEC ZMIENIONEGO WARUNKU ****

            _logger.LogDebug("NetworkManager.RequestGameServerConnectionAsync called. ServerId: {ServerId}. CurrentState: {State}", serverId, _currentState);

            // Sprawdzenie stanu początkowego (musi być ReceivedServerList)
            if (_currentState != ClientConnectionState.ReceivedServerList)
            {
                OnErrorOccurred($"Cannot initiate game server connection request from state: {_currentState}");
                return;
            }
            if (!_connectionManager.IsConnected) // Sprawdź połączenie z CS
            {
                OnErrorOccurred("Cannot request game server connection: Not connected to Connect Server.");
                return;
            }

            _logger.LogInformation("Requesting connection info for Server ID: {ServerId}", serverId);
            UpdateState(ClientConnectionState.RequestingConnectionInfo); // Ustaw stan PRZED wysłaniem
            await _connectServerService.RequestConnectionInfoAsync(serverId);
        }

        // **** DODAJ TĘ METODĘ ****
        // NetworkManager.cs
        public async Task RequestServerListAsync(bool initiatedByUi = false)
        {
            _logger.LogDebug("NetworkManager.RequestServerListAsync called. InitiatedByUi: {Initiated}. CurrentState: {State}", initiatedByUi, _currentState);

            // Sprawdzaj stan tylko dla UI lub jeśli nie jesteśmy połączeni
            if (initiatedByUi && _currentState != ClientConnectionState.ConnectedToConnectServer && _currentState != ClientConnectionState.ReceivedServerList)
            {
                OnErrorOccurred($"Cannot request server list in state: {_currentState}");
                return;
            }
            if (!_connectionManager.IsConnected) // Zawsze sprawdzaj połączenie
            {
                OnErrorOccurred("Cannot request server list: Not connected.");
                return;
            }

            // Aktualizuj stan tylko jeśli jeszcze nie jesteśmy w trakcie żądania
            // (Chociaż teraz jest to mniej krytyczne, bo wywołanie wewnętrzne dzieje się po zmianie stanu)
            if (_currentState != ClientConnectionState.RequestingServerList)
            {
                UpdateState(ClientConnectionState.RequestingServerList);
            }

            // Deleguj wysłanie do serwisu
            await _connectServerService.RequestServerListAsync();
        }

        // **** ZMODYFIKOWANA METODA ****
        /// <summary>
        /// Sends a login request using the provided username and password.
        /// </summary>
        /// <param name="username">Username from UI.</param>
        /// <param name="password">Password from UI.</param>
        public async Task SendLoginRequestAsync(string username, string password)
        {
            if (_currentState != ClientConnectionState.ConnectedToGameServer && _currentState != ClientConnectionState.Authenticating) // Zezwól też w Authenticating na ponowienie
            {
                OnErrorOccurred($"Cannot send login request in state: {_currentState}");
                return;
            }
            _logger.LogInformation("Sending login request for user: {Username}", username);
            UpdateState(ClientConnectionState.Authenticating);
            // Wywołaj serwis, przekazując dane z argumentów
            await _loginService.SendLoginRequestAsync(username, password);
        }
        // **** KONIEC ZMODYFIKOWANEJ METODY ****

        // Metoda do wysłania logowania z ustawień (jeśli nadal potrzebna do automatycznego logowania)
        public async Task SendLoginRequestFromSettingsAsync()
        {
            if (_currentState != ClientConnectionState.ConnectedToGameServer)
            {
                OnErrorOccurred($"Cannot send login request in state: {_currentState}");
                return;
            }
            _logger.LogInformation("Sending login request using settings for user: {Username}", _settings.Username);
            UpdateState(ClientConnectionState.Authenticating);
            await _loginService.SendLoginRequestAsync(_settings.Username, _settings.Password);
        }

        public async Task SendSelectCharacterRequestAsync(string characterName)
        {
            if (_currentState != ClientConnectionState.ConnectedToGameServer) // Should be called after char list received
            {
                OnErrorOccurred($"Cannot select character in state: {_currentState}");
                return;
            }
            _logger.LogInformation("Sending select character request for: {CharacterName}", characterName);
            UpdateState(ClientConnectionState.SelectingCharacter); // Update state
            await _characterService.SelectCharacterAsync(characterName);
        }

        // --- Internal State and Event Handling ---

        // NetworkManager.cs
        internal void UpdateState(ClientConnectionState newState)
        {
            if (_currentState != newState)
            {
                var oldState = _currentState;
                _logger.LogInformation(">>> UpdateState: Changing state from {OldState} to {NewState}", oldState, newState);
                _currentState = newState; // Set state immediately
                _logger.LogInformation("=== UpdateState: _currentState is now {CurrentState}", _currentState); // Log ZMIENIONY stan

                MuGame.ScheduleOnMainThread(() =>
                {
                    _logger.LogTrace("--- UpdateState: Raising ConnectionStateChanged event for state {NewState} on main thread.", newState);
                    try
                    {
                        ConnectionStateChanged?.Invoke(this, newState);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "--- UpdateState: Exception during ConnectionStateChanged event invocation for state {NewState}", newState);
                    }
                    _logger.LogTrace("<<< UpdateState: ConnectionStateChanged event raising scheduled/attempted on main thread.", newState);
                });
            }
            else
            {
                _logger.LogTrace(">>> UpdateState: State {NewState} is the same as current. No change.", newState);
            }
        }

        internal void OnErrorOccurred(string message)
        {
            _logger.LogError("Network Error: {Message}", message);
            MuGame.ScheduleOnMainThread(() =>
            {
                ErrorOccurred?.Invoke(this, message);
            });
        }

        internal void ProcessCharacterList(List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
            _logger.LogInformation(">>> ProcessCharacterList: Received list with {Count} characters. Raising event on UI thread...", characters?.Count ?? 0);
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogTrace("--- ProcessCharacterList: Raising CharacterListReceived event on main thread.");
                try
                {
                    // *** PASS THE UPDATED LIST TYPE ***
                    CharacterListReceived?.Invoke(this, characters ?? new()); // Pass empty list if null
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "--- ProcessCharacterList: Exception during CharacterListReceived event invocation.");
                }
                _logger.LogTrace("<<< ProcessCharacterList: CharacterListReceived event raising scheduled/attempted.");
            });
        }

        // Called by ConnectServerHandler
        internal void ProcessHelloPacket()
        {
            _logger.LogInformation("Processing Hello packet. Updating state and requesting server list.");
            UpdateState(ClientConnectionState.ConnectedToConnectServer);
            // Po zmianie stanu od razu wyślij żądanie listy serwerów
            _ = RequestServerListAsync(initiatedByUi: false); // Wywołaj jako nową Tasketę
        }

        // Called by ConnectServerHandler
        internal void StoreServerList(List<ServerInfo> servers)
        {
            // Zapisz otrzymaną listę w polu _serverList
            _serverList = servers ?? new List<ServerInfo>(); // Upewnij się, że nie jest null

            UpdateState(ClientConnectionState.ReceivedServerList);
            _logger.LogInformation("Server list received ({Count} servers) and cached.", _serverList.Count);

            // Podnieś event na głównym wątku
            MuGame.ScheduleOnMainThread(() =>
            {
                // Przekaż kopię lub widok read-only, aby UI nie modyfikowało oryginału
                ServerListReceived?.Invoke(this, _serverList.ToList());
            });
        }

        // Called by ConnectServerHandler
        internal async void SwitchToGameServer(string host, int port)
        {
            if (_currentState != ClientConnectionState.RequestingConnectionInfo && _currentState != ClientConnectionState.ReceivedConnectionInfo)
            {
                _logger.LogWarning("Received game server info in unexpected state ({CurrentState}). Ignoring.", _currentState);
                return;
            }
            UpdateState(ClientConnectionState.ReceivedConnectionInfo); // Mark as received

            _logger.LogInformation("Disconnecting from Connect Server...");
            var oldConnection = _connectionManager.Connection; // Capture before disconnect
            if (oldConnection != null)
            {
                try { oldConnection.PacketReceived -= HandlePacketAsync; } catch { /* Ignore */ }
                try { oldConnection.Disconnected -= HandleDisconnectAsync; } catch { /* Ignore */ }
            }
            await _connectionManager.DisconnectAsync(); // Disconnect from CS

            _logger.LogInformation("Connecting to Game Server {Host}:{Port}...", host, port);
            UpdateState(ClientConnectionState.ConnectingToGameServer);
            _packetRouter.SetRoutingMode(false); // Switch routing to GS mode

            if (await _connectionManager.ConnectAsync(host, (ushort)port, true, _managerCts?.Token ?? default))
            {
                var gsConnection = _connectionManager.Connection;
                gsConnection.PacketReceived += HandlePacketAsync;
                gsConnection.Disconnected += HandleDisconnectAsync;
                _connectionManager.StartReceiving(_managerCts?.Token ?? default); // Start listening on GS connection
                                                                                  // State updated to ConnectedToGameServer when F1 00 received
            }
            else
            {
                OnErrorOccurred($"Connection to Game Server {host}:{port} failed.");
                UpdateState(ClientConnectionState.Disconnected);
            }
        }

        // Called by MiscGamePacketHandler on F1 00
        internal void ProcessGameServerEntered()
        {
            _logger.LogInformation(">>> ProcessGameServerEntered: Received welcome packet. Calling UpdateState(ConnectedToGameServer)...");
            UpdateState(ClientConnectionState.ConnectedToGameServer);
            _logger.LogInformation("<<< ProcessGameServerEntered: UpdateState called.");
        }

        // Called by MiscGamePacketHandler on F1 01 (Success)
        internal void ProcessLoginSuccess()
        {
            _logger.LogInformation(">>> ProcessLoginSuccess: Login OK. Updating state back to ConnectedToGameServer and requesting character list...");
            // Zmień stan z powrotem, aby umożliwić wybór postaci
            UpdateState(ClientConnectionState.ConnectedToGameServer);
            // Podnieś event LoginSuccess dla UI
            MuGame.ScheduleOnMainThread(() => LoginSuccess?.Invoke(this, EventArgs.Empty));
            // Wyślij żądanie listy postaci
            _ = _characterService.RequestCharacterListAsync();
            _logger.LogInformation("<<< ProcessLoginSuccess: State updated and CharacterList requested.");
        }

        // Called by MiscGamePacketHandler on F1 01
        // Metoda ProcessLoginResponse teraz tylko decyduje co dalej
        internal void ProcessLoginResponse(LoginResponse.LoginResult result)
        {
            if (result == LoginResponse.LoginResult.Okay)
            {
                // Wywołaj nową metodę dla sukcesu
                ProcessLoginSuccess();
            }
            else
            {
                // Logika dla nieudanego logowania pozostaje
                string reason = result.ToString();
                _logger.LogError("Login failed: {Reason}", reason);
                OnErrorOccurred($"Login failed: {reason}");
                MuGame.ScheduleOnMainThread(() => LoginFailed?.Invoke(this, EventArgs.Empty));
                UpdateState(ClientConnectionState.ConnectedToGameServer); // Pozwól na ponowną próbę
            }
        }

        // Called by CharacterDataHandler on F3 03
        internal void ProcessCharacterInformation()
        {
            if (_currentState != ClientConnectionState.SelectingCharacter) { /* ... log warning ... */ return; }

            _logger.LogInformation(">>> ProcessCharacterInformation: Character selected successfully. Updating state to InGame and raising EnteredGame event...");
            UpdateState(ClientConnectionState.InGame); // Zmień stan (to zaplanuje ConnectionStateChanged)

            _logger.LogInformation("--- ProcessCharacterInformation: Raising EnteredGame event directly...");
            try { EnteredGame?.Invoke(this, EventArgs.Empty); } // Podnieś event od razu
            catch (Exception ex) { _logger.LogError(ex, "--- ProcessCharacterInformation: Exception during EnteredGame event invocation."); }
            _logger.LogInformation("<<< ProcessCharacterInformation: State updated and EnteredGame event raised.");
        }


        // --- Packet Handling ---

        private ValueTask HandlePacketAsync(ReadOnlySequence<byte> sequence)
        {
            // Route packet using the router instance
            // The router now knows about 'this' NetworkManager if needed
            return new ValueTask(_packetRouter.RoutePacketAsync(sequence));
        }

        private ValueTask HandleDisconnectAsync()
        {
            _logger.LogWarning("Connection lost.");
            UpdateState(ClientConnectionState.Disconnected);
            // Optionally clear server/character lists
            MuGame.ScheduleOnMainThread(() =>
            {
                ServerListReceived?.Invoke(this, new List<ServerInfo>());
                // *** PASS EMPTY LIST OF CORRECT TUPLE TYPE ***
                CharacterListReceived?.Invoke(this, new List<(string Name, CharacterClassNumber Class, ushort Level)>()); // Update this line
            });

            return new ValueTask(_packetRouter.OnDisconnected()); // Notify router
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing NetworkManager...");
            _managerCts?.Cancel();
            await _connectionManager.DisposeAsync();
            _managerCts?.Dispose();
            _logger.LogInformation("NetworkManager disposed.");
            GC.SuppressFinalize(this);
        }
    }
}