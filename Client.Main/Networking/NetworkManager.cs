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

        private ClientConnectionState _currentState = ClientConnectionState.Initial;
        private List<ServerInfo> _serverList = new();
        private CancellationTokenSource? _managerCts; // For overall manager cancellation

        // --- Events for UI Communication ---
        public event EventHandler<ClientConnectionState>? ConnectionStateChanged;
        public event EventHandler<List<ServerInfo>>? ServerListReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<List<(string Name, CharacterClassNumber Class)>>? CharacterListReceived;
        public event EventHandler? LoginSuccess; // Specific event for successful login
        public event EventHandler? LoginFailed; // Specific event for failed login
        public event EventHandler? EnteredGame; // Specific event for successful character selection
        // Add more events as needed (e.g., ChatMessageReceived, CharacterStatsUpdated)

        // --- Public Properties ---
        public ClientConnectionState CurrentState => _currentState;
        public bool IsConnected => _connectionManager.IsConnected;

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
            _logger.LogDebug("NetworkManager.RequestGameServerConnectionAsync called. ServerId: {ServerId}. CurrentState: {State}", serverId, _currentState);

            // Ten warunek jest poprawny - sprawdzamy CZY MOŻNA zainicjować żądanie
            if (_currentState != ClientConnectionState.ReceivedServerList)
            {
                // Ten OnErrorOccurred jest POPRAWNY - informuje o błędnej próbie wywołania
                OnErrorOccurred($"Cannot initiate game server connection request in state: {_currentState}");
                return;
            }
            // Sprawdź też połączenie z Connect Serverem
            if (!_connectionManager.IsConnected)
            {
                OnErrorOccurred("Cannot request game server connection: Not connected to Connect Server.");
                return;
            }

            _logger.LogInformation("Requesting connection info for Server ID: {ServerId}", serverId);
            // Zmień stan OD RAZU, ZANIM wywołasz serwis
            UpdateState(ClientConnectionState.RequestingConnectionInfo);
            // Wywołaj serwis, który wyśle pakiet
            await _connectServerService.RequestConnectionInfoAsync(serverId);

            // Upewnij się, że TUTAJ NIE MA już żadnego OnErrorOccurred !!!
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
                _logger.LogInformation("State changed: {OldState} -> {NewState}", oldState, newState);
                // **** ZMIANA: Ustaw stan OD RAZU ****
                _currentState = newState;

                // Zaplanuj tylko podniesienie eventu dla UI na główny wątek
                MuGame.ScheduleOnMainThread(() =>
                {
                    _logger.LogTrace("Raising ConnectionStateChanged event for state {NewState} on main thread.", newState);
                    ConnectionStateChanged?.Invoke(this, newState);

                    // **** PRZENIESIONA LOGIKA REAKCJI NA ZMIANĘ STANU ****
                    // Reaguj na zmianę stanu TUTAJ, w głównym wątku, PO zmianie _currentState
                    if (newState == ClientConnectionState.ConnectedToConnectServer)
                    {
                        _logger.LogInformation("State is now ConnectedToConnectServer (on main thread), requesting server list...");
                        // Wywołaj asynchronicznie, aby nie blokować pętli Update
                        _ = Task.Run(() => RequestServerListAsync(initiatedByUi: false));
                    }
                    // Dodaj reakcję na ConnectedToGameServer, jeśli chcesz automatyczne logowanie z ustawień
                    // else if (newState == ClientConnectionState.ConnectedToGameServer)
                    // {
                    //     _logger.LogInformation("State is now ConnectedToGameServer (on main thread), attempting auto-login from settings...");
                    //     _ = Task.Run(() => SendLoginRequestFromSettingsAsync());
                    // }
                });
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

        // Called by ConnectServerHandler
        internal void ProcessHelloPacket()
        {
            _logger.LogInformation("Processing Hello packet. Updating state.");
            // Od razu zmień stan. Reakcja (wysłanie RequestServerList) nastąpi w UpdateState.
            UpdateState(ClientConnectionState.ConnectedToConnectServer);
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
            _logger.LogInformation("Received welcome packet from Game Server. Ready to log in.");
            UpdateState(ClientConnectionState.ConnectedToGameServer);
            // Usunięto automatyczne wywołanie logowania stąd.
            // Logowanie nastąpi po interakcji użytkownika w LoginDialog.
            // Jeśli CHCESZ próbować automatycznego logowania z ustawień, możesz tu wywołać:
            // _ = SendLoginRequestFromSettingsAsync();
        }

        // Called by MiscGamePacketHandler on F1 01
        internal void ProcessLoginResponse(LoginResponse.LoginResult result)
        {
            if (result == LoginResponse.LoginResult.Okay)
            {
                _logger.LogInformation("Login successful. Requesting character list.");
                LoginSuccess?.Invoke(this, EventArgs.Empty); // Raise event for UI
                                                             // Request character list automatically after successful login
                _ = _characterService.RequestCharacterListAsync();
            }
            else
            {
                string reason = result.ToString();
                _logger.LogError("Login failed: {Reason}", reason);
                OnErrorOccurred($"Login failed: {reason}");
                LoginFailed?.Invoke(this, EventArgs.Empty); // Raise event for UI
                                                            // Consider disconnecting or allowing retry? For now, stay ConnectedToGameServer.
                UpdateState(ClientConnectionState.ConnectedToGameServer); // Revert state
            }
        }

        // Called by MiscGamePacketHandler on F3 00
        internal void ProcessCharacterList(List<(string Name, CharacterClassNumber Class)> characters)
        {
            _logger.LogInformation("Character list received ({Count} characters).", characters.Count);
            // State remains ConnectedToGameServer until character is selected
            // Raise event on UI thread
            MuGame.ScheduleOnMainThread(() =>
            {
                CharacterListReceived?.Invoke(this, characters);
            });
        }

        // Called by CharacterDataHandler on F3 03
        internal void ProcessCharacterInformation()
        {
            _logger.LogInformation("Successfully entered game world with character: {Name}", _characterState.Name);
            UpdateState(ClientConnectionState.InGame);
            EnteredGame?.Invoke(this, EventArgs.Empty);
            // ScopeManager and CharacterState are updated by the handlers directly
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
                CharacterListReceived?.Invoke(this, new List<(string Name, CharacterClassNumber Class)>()); // Clear UI list
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