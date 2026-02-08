using Client.Main.Configuration;
using Client.Main.Controls;
using Client.Main.Core.Client;
using Client.Main.Core.Models;
using Client.Main.Networking.PacketHandling;
using Client.Main.Networking.Services;
using Client.Main.Scenes;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets; // For CharacterClassNumber
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.SimpleModulus;
using MUnique.OpenMU.Network.Xor;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Networking
{
    public sealed class NetworkManager : IAsyncDisposable
    {
        // Fields
        private static readonly SimpleModulusKeys EncryptKeys = PipelinedSimpleModulusEncryptor.DefaultClientKey;
        private static readonly SimpleModulusKeys DecryptKeys = PipelinedSimpleModulusDecryptor.DefaultClientKey;
        private static readonly byte[] Xor3Keys = DefaultKeys.Xor3Keys;
        private const int ClientVersionLength = 5;
        private const int ClientSerialLength = 16;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<NetworkManager> _logger;
        private readonly MuOnlineSettings _settings;
        private readonly ConnectionManager _connectionManager;
        private readonly PacketRouter _packetRouter;
        private readonly LoginService _loginService;
        private readonly CharacterService _characterService;
        private readonly ConnectServerService _connectServerService;
        private readonly CharacterState _characterState;
        private readonly PartyManager _partyManager;
        private readonly ScopeManager _scopeManager;
        private readonly Dictionary<byte, byte> _serverDirectionMap;
        private string _selectedCharacterNameForLogin = string.Empty;
        private readonly Ping _pingSender = new Ping();

        private ClientConnectionState _currentState = ClientConnectionState.Initial;
        private List<ServerInfo> _serverList = new();
        private List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _lastCharacterList;
        private CancellationTokenSource _managerCts;

        private string _currentHost = string.Empty; // Host of current connection
        private int _currentPort; // Port of current connection

        // Events
        public event EventHandler<ClientConnectionState> ConnectionStateChanged;
        public event EventHandler<List<ServerInfo>> ServerListReceived;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)>> CharacterListReceived;
        public event EventHandler LoginSuccess;
        public event EventHandler EnteredGame;
        public event EventHandler<LoginResponse.LoginResult> LoginFailed;
        public event EventHandler<LogOutType> LogoutResponseReceived;

        // Properties
        public ClientConnectionState CurrentState => _currentState;
        public bool IsConnected => _connectionManager.IsConnected;
        public CharacterState GetCharacterState() => _characterState;
        public Task SendClientReadyAfterMapChangeAsync()
            => _characterService.SendClientReadyAfterMapChangeAsync();

        public CharacterService GetCharacterService() => _characterService;
        public ScopeManager GetScopeManager() => _scopeManager;
        public PartyManager GetPartyManager() => _partyManager;
        public TargetProtocolVersion TargetVersion => _packetRouter.TargetVersion;
        public string CurrentHost => _currentHost;
        public int CurrentPort => _currentPort;
        public IReadOnlyList<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> GetCachedCharacterList()
            => _lastCharacterList is null ? Array.Empty<(string, CharacterClassNumber, ushort, byte[])>() : new List<(string, CharacterClassNumber, ushort, byte[])>(_lastCharacterList);

        // Constructors
        public NetworkManager(ILoggerFactory loggerFactory, MuOnlineSettings settings, CharacterState characterState, ScopeManager scopeManager)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<NetworkManager>();
            _settings = settings;
            _characterState = characterState;
            _scopeManager = scopeManager;
            _partyManager = new PartyManager(_loggerFactory);

            var clientVersionBytes = BuildClientVersionBytes(settings.ClientVersion, _logger);
            var clientSerialBytes = BuildClientSerialBytes(settings.ClientSerial, _logger);
            var targetVersion = System.Enum.Parse<TargetProtocolVersion>(settings.ProtocolVersion, ignoreCase: true);

            _connectionManager = new ConnectionManager(loggerFactory, EncryptKeys, DecryptKeys);

            _loginService = new LoginService(_connectionManager, _loggerFactory.CreateLogger<LoginService>(), clientVersionBytes, clientSerialBytes, Xor3Keys);
            _characterService = new CharacterService(_connectionManager, _loggerFactory.CreateLogger<CharacterService>());
            _connectServerService = new ConnectServerService(_connectionManager, _loggerFactory.CreateLogger<ConnectServerService>());

            _packetRouter = new PacketRouter(loggerFactory, _characterService, _loginService, targetVersion, this, _characterState, _scopeManager, _partyManager, _settings);

            _managerCts = new CancellationTokenSource();

            _serverDirectionMap = settings.DirectionMap?.ToDictionary(kv => Convert.ToByte(kv.Key), kv => kv.Value)
                                  ?? new Dictionary<byte, byte>();
        }

        // Public Methods
        public IReadOnlyDictionary<byte, byte> GetDirectionMap()
        {
            return _serverDirectionMap;
        }

        /// <summary>
        /// Sends a public chat message (including party, guild, gens with prefixes) to the server.
        /// </summary>
        /// <param name="message">The message content, potentially including prefixes like ~, @, $.</param>
        public async Task SendPublicChatMessageAsync(string message)
        {
            if (!IsConnected || _currentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot send public chat message, not connected or not in game. State: {State}", _currentState);
                return;
            }

            string characterName = _characterState.Name ?? "Unknown";
            if (characterName == "???" || characterName == "Unknown")
            {
                _logger.LogWarning("Cannot send public chat message, character name not available yet.");
                OnErrorOccurred("Cannot send chat message: Character data not loaded.");
                return;
            }

            _logger.LogInformation("✉️ Sending Public Chat: '{Message}'", message);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                    PacketBuilder.BuildPublicChatMessagePacket(_connectionManager.Connection.Output, characterName, message)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error sending public chat message.");
                OnErrorOccurred("Error sending chat message.");
            }
        }

        /// <summary>
        /// Sends a whisper message to the specified receiver.
        /// </summary>
        /// <param name="receiver">The name of the character to receive the whisper.</param>
        /// <param name="message">The message content.</param>
        public async Task SendWhisperMessageAsync(string receiver, string message)
        {
            if (!IsConnected || _currentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot send whisper message, not connected or not in game. State: {State}", _currentState);
                return;
            }

            _logger.LogInformation("Sending Whisper to '{Receiver}': '{Message}'", receiver, message);
            try
            {
                await _connectionManager.Connection.SendAsync(() =>
                   PacketBuilder.BuildWhisperMessagePacket(_connectionManager.Connection.Output, receiver, message)
               );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error sending whisper message to {Receiver}.", receiver);
                OnErrorOccurred($"Error sending whisper to {receiver}.");
            }
        }

        /// <summary>
        /// Gets a read-only view of the currently cached server list.
        /// </summary>
        /// <returns>A read-only list of ServerInfo objects.</returns>
        public IReadOnlyList<ServerInfo> GetCachedServerList()
        {
            // Return a copy or ReadOnly view to prevent external modification.
            // ToList() creates a new list (a copy).
            // Or use ReadOnlyCollection for a more efficient read-only view:
            return new ReadOnlyCollection<ServerInfo>(_serverList);
        }

        public Task SendWalkRequestAsync(byte startX, byte startY, byte[] path)
            => _characterService.SendWalkRequestAsync(startX, startY, path);

        public Task SendInstantMoveRequestAsync(byte x, byte y)
            => _characterService.SendInstantMoveRequestAsync(x, y);

        public Task SendHitRequestAsync(ushort targetId, byte attackAnimation, byte lookingDirection)
            => _characterService.SendHitRequestAsync(targetId, attackAnimation, lookingDirection);

        /// <summary>
        /// Sends a warp command request to the server.
        /// </summary>
        /// <param name="warpInfoIndex">The index of the warp destination.</param>
        /// <param name="commandKey">Optional command key, if required by the server (default is 0).</param>
        public async Task SendWarpRequestAsync(ushort warpInfoIndex, uint commandKey = 0)
        {
            if (!IsConnected || _currentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot send warp request, not connected or not in game. State: {State}", _currentState);
                OnErrorOccurred("Cannot warp: Not in game.");
                return;
            }

            await _characterService.SendWarpCommandRequestAsync(warpInfoIndex, commandKey);
        }

        public async Task ConnectToConnectServerAsync()
        {
            if (_connectionManager.IsConnected)
            {
                OnErrorOccurred("Already connected. Disconnect first.");
                return;
            }

            var cancellationToken = _managerCts.Token;
            if (cancellationToken.IsCancellationRequested) return;

            UpdateState(ClientConnectionState.ConnectingToConnectServer);
            _packetRouter.SetRoutingMode(true); // Set routing to CS mode

            if (await _connectionManager.ConnectAsync(_settings.ConnectServerHost, _settings.ConnectServerPort, false, cancellationToken))
            {
                _currentHost = _settings.ConnectServerHost;
                _currentPort = _settings.ConnectServerPort;

                var csConnection = _connectionManager.Connection;
                csConnection.PacketReceived += HandlePacketAsync;
                csConnection.Disconnected += HandleDisconnectAsync;
                _connectionManager.StartReceiving(cancellationToken);
            }
            else
            {
                OnErrorOccurred($"Connection to Connect Server {_settings.ConnectServerHost}:{_settings.ConnectServerPort} failed.");
                UpdateState(ClientConnectionState.Disconnected);
            }
        }

        /// <summary>
        /// Disconnects any existing Connect Server connection and reconnects using the current settings.
        /// Useful when host/port are changed at runtime (e.g., from UI).
        /// </summary>
        public async Task ForceReconnectToConnectServerAsync()
        {
            _logger.LogInformation("Force reconnecting to Connect Server {Host}:{Port}", _settings.ConnectServerHost, _settings.ConnectServerPort);

            try
            {
                await _connectionManager.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting before reconnect.");
            }

            _serverList.Clear();
            _currentHost = string.Empty;
            _currentPort = 0;
            UpdateState(ClientConnectionState.Disconnected);

            await ConnectToConnectServerAsync();
        }

        /// <summary>
        /// Updates the connect server host/port used for subsequent connections.
        /// </summary>
        public void UpdateConnectServerSettings(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            {
                _logger.LogWarning("Ignoring invalid connect server settings: {Host}:{Port}", host, port);
                return;
            }

            _logger.LogInformation("Applying connect server settings: {Host}:{Port}", host, port);
            _settings.ConnectServerHost = host;
            _settings.ConnectServerPort = port;
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
            if (_currentState >= ClientConnectionState.RequestingConnectionInfo && _currentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("RequestGameServerConnectionAsync called while already connecting/connected to GS or requesting info. Ignoring request for ServerId: {ServerId}. CurrentState: {State}", serverId, _currentState);
                return; // Already in progress or this stage is complete, ignore
            }

            _logger.LogDebug("NetworkManager.RequestGameServerConnectionAsync called. ServerId: {ServerId}. CurrentState: {State}", serverId, _currentState);

            if (_currentState != ClientConnectionState.ReceivedServerList)
            {
                OnErrorOccurred($"Cannot initiate game server connection request from state: {_currentState}");
                return;
            }
            if (!_connectionManager.IsConnected)
            {
                OnErrorOccurred("Cannot request game server connection: Not connected to Connect Server.");
                return;
            }

            _logger.LogInformation("Requesting connection info for Server ID: {ServerId}", serverId);
            UpdateState(ClientConnectionState.RequestingConnectionInfo);
            await _connectServerService.RequestConnectionInfoAsync(serverId);
        }

        public async Task RequestServerListAsync(bool initiatedByUi = false)
        {
            _logger.LogDebug("NetworkManager.RequestServerListAsync called. InitiatedByUi: {Initiated}. CurrentState: {State}", initiatedByUi, _currentState);

            if (initiatedByUi && _currentState != ClientConnectionState.ConnectedToConnectServer && _currentState != ClientConnectionState.ReceivedServerList)
            {
                OnErrorOccurred($"Cannot request server list in state: {_currentState}");
                return;
            }
            if (!_connectionManager.IsConnected)
            {
                OnErrorOccurred("Cannot request server list: Not connected.");
                return;
            }

            if (_currentState != ClientConnectionState.RequestingServerList)
            {
                UpdateState(ClientConnectionState.RequestingServerList);
            }
            await _connectServerService.RequestServerListAsync();
        }

        /// <summary>
        /// Sends a login request using the provided username and password.
        /// </summary>
        /// <param name="username">Username from UI.</param>
        /// <param name="password">Password from UI.</param>
        public async Task SendLoginRequestAsync(string username, string password)
        {
            if (_currentState != ClientConnectionState.ConnectedToGameServer && _currentState != ClientConnectionState.Authenticating) // Allow retry in Authenticating state
            {
                OnErrorOccurred($"Cannot send login request in state: {_currentState}");
                return;
            }
            _logger.LogInformation("Sending login request for user: {Username}", username);
            UpdateState(ClientConnectionState.Authenticating);
            await _loginService.SendLoginRequestAsync(username, password);
        }

        public async Task SendSelectCharacterRequestAsync(string characterName)
        {
            if (_currentState != ClientConnectionState.ConnectedToGameServer)
            {
                OnErrorOccurred($"Cannot select character in state: {_currentState}");
                return;
            }
            _logger.LogInformation("Sending select character request for: {CharacterName}", characterName);
            _selectedCharacterNameForLogin = characterName;
            UpdateState(ClientConnectionState.SelectingCharacter);
            await _characterService.SelectCharacterAsync(characterName);
        }

        // Internal Methods
        internal void ProcessCharacterRespawn(ushort mapId, byte x, byte y, byte direction)
        {
            _logger.LogInformation("ProcessCharacterRespawn: MapID={MapId}, X={X}, Y={Y}, Direction={Direction}", mapId, x, y, direction);

            bool previousInGameStatus = _characterState.IsInGame;
            _characterState.UpdateMap(mapId);
            _characterState.UpdatePosition(x, y);
            _characterState.IsInGame = true;

            MuGame.ScheduleOnMainThread(async () =>
            {
                try
                {
                    var game = MuGame.Instance;
                    // Avoid racing with SelectCharacterScene which will switch to GameScene
                    if (game?.ActiveScene is SelectCharacterScene)
                    {
                        _logger.LogInformation("ProcessCharacterRespawn: Currently in SelectCharacterScene; deferring scene change to it.");
                        return;
                    }
                    if (game?.ActiveScene is not GameScene gs)
                    {
                        _logger.LogWarning("ProcessCharacterRespawn: ActiveScene is not GameScene. Changing scene.");
                        game.ChangeScene(new GameScene());
                        return;
                    }

                    var currentWorld = gs.World as WalkableWorldControl;
                    bool mapChanged = currentWorld == null || currentWorld.MapId != mapId;

                    _logger.LogDebug("ProcessCharacterRespawn: CurrentWorldIndex: {CurrentIdx}, NewMapId: {NewMapId}, MapChanged: {MapChangedFlag}",
                        currentWorld?.WorldIndex, mapId, mapChanged);

                    var hero = gs.Hero;
                    hero.Reset();
                    hero.Location = new Vector2(x, y);
                    hero.Direction = (Client.Main.Models.Direction)direction;
                    // Synchronize MoveTargetPosition to prevent walk animation after teleport
                    hero.MoveTargetPosition = hero.TargetPosition;
                    hero.Position = hero.TargetPosition;

                    if (mapChanged)
                    {
                        _logger.LogInformation("ProcessCharacterRespawn: Map has changed. Requesting world change to map {MapId}", mapId);
                        if (GameScene.MapWorldRegistry.TryGetValue((byte)mapId, out var worldType))
                        {
                            await gs.ChangeMap(worldType);
                        }
                        else
                        {
                            _logger.LogWarning("ProcessCharacterRespawn: Unknown mapId {MapId} for world change. Creating new GameScene.", mapId);
                            game.ChangeScene(new GameScene());
                        }
                    }
                    else
                    {
                        _logger.LogInformation("ProcessCharacterRespawn: Same map ({MapId}). Updating hero position.", mapId);
                        _logger.LogInformation("ProcessCharacterRespawn: Sending ClientReadyAfterMapChange for same map teleport/respawn.");
                        await _characterService.SendClientReadyAfterMapChangeAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ProcessCharacterRespawn.");
                }
            });
        }

        internal void UpdateState(ClientConnectionState newState)
        {
            if (_currentState != newState)
            {
                var oldState = _currentState;
                _logger.LogInformation(">>> UpdateState: Changing state from {OldState} to {NewState}", oldState, newState);
                _currentState = newState;
                _logger.LogInformation("=== UpdateState: _currentState is now {CurrentState}", _currentState);

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
                    _logger.LogTrace("<<< UpdateState: ConnectionStateChanged event raising scheduled/attempted on main thread.");
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

        internal void ProcessCharacterList(List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters)
        {            // If we receive a character list while in-game (e.g., returning to character selection),
            // normalize the state back to ConnectedToGameServer so UI can select a character without rejection.
            if (_currentState == ClientConnectionState.InGame)
            {
                _logger.LogInformation("ProcessCharacterList: Current state is InGame; switching to ConnectedToGameServer for character selection.");
                UpdateState(ClientConnectionState.ConnectedToGameServer);
            }
            // Cache the last received list for potential fallback usage
            try { _lastCharacterList = characters?.Select(c => (c.Name, c.Class, c.Level, c.Appearance?.ToArray() ?? Array.Empty<byte>())).ToList() ?? new(); }
            catch { _lastCharacterList = characters ?? new(); }
            _logger.LogInformation(">>> ProcessCharacterList: Received list with {Count} characters. Raising event on UI thread...", characters?.Count ?? 0);
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogTrace("--- ProcessCharacterList: Raising CharacterListReceived event on main thread.");
                try
                {
                    CharacterListReceived?.Invoke(this, characters ?? new());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "--- ProcessCharacterList: Exception during CharacterListReceived event invocation.");
                }
                _logger.LogTrace("<<< ProcessCharacterList: CharacterListReceived event raising scheduled/attempted.");
            });
        }

        internal void ProcessHelloPacket()
        {
            _logger.LogInformation("Processing Hello packet. Updating state and requesting server list.");
            UpdateState(ClientConnectionState.ConnectedToConnectServer);
            _ = RequestServerListAsync(initiatedByUi: false);
        }

        internal void StoreServerList(List<ServerInfo> servers)
        {
            _serverList = servers ?? new List<ServerInfo>(); // Ensure it's not null
            UpdateState(ClientConnectionState.ReceivedServerList);
            _logger.LogInformation("Server list received ({Count} servers) and cached.", _serverList.Count);

            MuGame.ScheduleOnMainThread(() =>
            {
                ServerListReceived?.Invoke(this, _serverList.ToList());
            });
        }

        internal async void SwitchToGameServer(string host, int port)
        {
            if (_currentState != ClientConnectionState.RequestingConnectionInfo && _currentState != ClientConnectionState.ReceivedConnectionInfo)
            {
                _logger.LogWarning("Received game server info in unexpected state ({CurrentState}). Ignoring.", _currentState);
                return;
            }
            UpdateState(ClientConnectionState.ReceivedConnectionInfo);

            _logger.LogInformation("Disconnecting from Connect Server...");
            var oldConnection = _connectionManager.Connection;
            if (oldConnection != null)
            {
                try { oldConnection.PacketReceived -= HandlePacketAsync; } catch { /* Ignore */ }
                try { oldConnection.Disconnected -= HandleDisconnectAsync; } catch { /* Ignore */ }
            }
            await _connectionManager.DisconnectAsync();

            _logger.LogInformation("Connecting to Game Server {Host}:{Port}...", host, port);
            UpdateState(ClientConnectionState.ConnectingToGameServer);
            _packetRouter.SetRoutingMode(false);

            if (await _connectionManager.ConnectAsync(host, (ushort)port, true, _managerCts.Token))
            {
                _currentHost = host;
                _currentPort = port;

                var gsConnection = _connectionManager.Connection;
                gsConnection.PacketReceived += HandlePacketAsync;
                gsConnection.Disconnected += HandleDisconnectAsync;
                _connectionManager.StartReceiving(_managerCts.Token);
            }
            else
            {
                OnErrorOccurred($"Connection to Game Server {host}:{port} failed.");
                UpdateState(ClientConnectionState.Disconnected);
            }
        }

        internal void ProcessGameServerEntered()
        {
            _logger.LogInformation(">>> ProcessGameServerEntered: Received welcome packet. Calling UpdateState(ConnectedToGameServer)...");
            UpdateState(ClientConnectionState.ConnectedToGameServer);
            _logger.LogInformation("<<< ProcessGameServerEntered: UpdateState called.");
        }

        internal void ProcessLoginSuccess()
        {
            _logger.LogInformation(">>> ProcessLoginSuccess: Login OK. Updating state back to ConnectedToGameServer and requesting character list...");
            // Change state back to allow character selection
            UpdateState(ClientConnectionState.ConnectedToGameServer);
            // Raise LoginSuccess event for UI
            MuGame.ScheduleOnMainThread(() => LoginSuccess?.Invoke(this, EventArgs.Empty));
            // Send character list request
            _ = _characterService.RequestCharacterListAsync();
            _logger.LogInformation("<<< ProcessLoginSuccess: State updated and CharacterList requested.");
        }

        internal void ProcessLoginResponse(LoginResponse.LoginResult result)
        {
            if (result == LoginResponse.LoginResult.Okay)
            {
                ProcessLoginSuccess();
            }
            else
            {
                string reasonString = result.ToString();
                _logger.LogError("Login failed: {ReasonString}", reasonString);
                OnErrorOccurred($"Login failed: {reasonString}");
                MuGame.ScheduleOnMainThread(() =>
                {
                    _logger.LogDebug("NetworkManager: Invoking LoginFailed event with reason: {ResultEnum} (Value: {ResultByte})", result, (byte)result);
                    LoginFailed?.Invoke(this, result);
                });
                UpdateState(ClientConnectionState.ConnectedToGameServer); // Allow retry
            }
        }

        internal async Task ProcessLogoutResponseAsync(LogOutType type)
        {
            _logger.LogInformation(">>> ProcessLogoutResponse: Received logout response type {Type}", type);
            _characterState.IsInGame = false;

            switch (type)
            {
                case LogOutType.BackToCharacterSelection:
                    UpdateState(ClientConnectionState.ConnectedToGameServer);
                    try
                    {
                        await _characterService.RequestCharacterListAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error requesting character list after logout response.");
                    }
                    break;

                case LogOutType.BackToServerSelection:
                    try
                    {
                        await _connectionManager.DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error while disconnecting from game server during logout to server selection.");
                    }

                    UpdateState(ClientConnectionState.Disconnected);
                    break;

                case LogOutType.CloseGame:
                    _logger.LogInformation("Logout type CloseGame received. No additional action taken by client.");
                    break;
            }

            MuGame.ScheduleOnMainThread(() =>
            {
                try
                {
                    LogoutResponseReceived?.Invoke(this, type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notifying LogoutResponseReceived subscribers.");
                }
            });

            _logger.LogInformation("<<< ProcessLogoutResponse: Handled logout response type {Type}", type);
        }

        internal void ProcessCharacterInformation()
        {
            if (_currentState != ClientConnectionState.SelectingCharacter)
            {
                _logger.LogWarning("ProcessCharacterInformation called in unexpected state ({CurrentState}). Character name might not be set correctly.", _currentState);
            }
            else if (string.IsNullOrEmpty(_selectedCharacterNameForLogin))
            {
                _logger.LogError("ProcessCharacterInformation called, but _selectedCharacterNameForLogin is null or empty. Cannot set character name in state.");
            }
            else
            {
                _characterState.Name = _selectedCharacterNameForLogin;
                _logger.LogInformation("CharacterState.Name set to '{Name}' based on selection.", _characterState.Name);
                _selectedCharacterNameForLogin = string.Empty; // Clear the temporary storage
            }

            _logger.LogInformation(">>> ProcessCharacterInformation: Character selected successfully. Updating state to InGame and raising EnteredGame event...");
            UpdateState(ClientConnectionState.InGame); // Change state (this will schedule ConnectionStateChanged)

            _logger.LogInformation("--- ProcessCharacterInformation: Raising EnteredGame event directly...");
            try
            {
                EnteredGame?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { _logger.LogError(ex, "--- ProcessCharacterInformation: Exception during EnteredGame event invocation."); }

            // Legacy quest state list (A0) - required for classic class-change quest UI (Sebina/Marlon/Devin).
            _ = _characterService.RequestLegacyQuestStateListAsync();

            _logger.LogInformation("<<< ProcessCharacterInformation: State updated and EnteredGame event raised.");
        }

        /// <summary>
        /// Pings the current server.
        /// </summary>
        /// <param name="timeoutMs">The timeout for the ping in milliseconds.</param>
        /// <returns>The round-trip time in milliseconds, or null if the ping failed or host is not set.</returns>
        public async Task<int?> PingServerAsync(int timeoutMs = 1000)
        {
            if (string.IsNullOrWhiteSpace(_currentHost))
                return null;

            try
            {
                var reply = await _pingSender.SendPingAsync(_currentHost, timeoutMs);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success
                    ? (int?)reply.RoundtripTime
                    : null;
            }
            catch
            {
                return null;
            }
        }

        // Private Methods
        private ValueTask HandlePacketAsync(ReadOnlySequence<byte> sequence)
        {
            return new ValueTask(_packetRouter.RoutePacketAsync(sequence));
        }

        private ValueTask HandleDisconnectAsync()
        {
            var previousState = _currentState;
            _logger.LogWarning("Connection lost. Previous state: {PreviousState}", previousState);
            UpdateState(ClientConnectionState.Disconnected);

            return new ValueTask(_packetRouter.OnDisconnected());
        }

        private static byte[] BuildClientVersionBytes(string clientVersion, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(clientVersion))
            {
                logger.LogWarning("ClientVersion is empty; defaulting to 00000 for protocol handshake.");
                return new byte[ClientVersionLength];
            }

            string trimmed = clientVersion.Trim();

            if (IsFiveDigitVersion(trimmed))
            {
                return Encoding.ASCII.GetBytes(trimmed);
            }

            if (TryNormalizeClientVersion(trimmed, out string normalized))
            {
                logger.LogInformation("Normalized ClientVersion '{Input}' -> '{Normalized}'.", trimmed, normalized);
                return Encoding.ASCII.GetBytes(normalized);
            }

            logger.LogWarning("ClientVersion '{Input}' not recognized; using padded ASCII bytes.", trimmed);
            return ToFixedLengthAsciiBytes(trimmed, ClientVersionLength);
        }

        private static byte[] BuildClientSerialBytes(string clientSerial, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(clientSerial))
            {
                logger.LogWarning("ClientSerial is empty; defaulting to zeros for protocol handshake.");
                return new byte[ClientSerialLength];
            }

            string trimmed = clientSerial.Trim();
            var bytes = Encoding.ASCII.GetBytes(trimmed);
            if (bytes.Length == ClientSerialLength)
            {
                return bytes;
            }

            if (bytes.Length > ClientSerialLength)
            {
                logger.LogWarning("ClientSerial '{Input}' exceeds {Length} bytes; truncating.", trimmed, ClientSerialLength);
                var truncated = new byte[ClientSerialLength];
                Array.Copy(bytes, truncated, ClientSerialLength);
                return truncated;
            }

            logger.LogWarning("ClientSerial '{Input}' shorter than {Length} bytes; padding with zeros.", trimmed, ClientSerialLength);
            var padded = new byte[ClientSerialLength];
            Array.Copy(bytes, padded, bytes.Length);
            return padded;
        }

        private static bool IsFiveDigitVersion(string value)
        {
            if (value.Length != ClientVersionLength)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch < '0' || ch > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryNormalizeClientVersion(string input, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string trimmed = input.Trim();
            if (IsFiveDigitVersion(trimmed))
            {
                normalized = trimmed;
                return true;
            }

            string[] parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (!TryParseSingleDigit(parts[0], out int major))
                {
                    return false;
                }

                int minor;
                int patch;
                if (parts.Length == 2)
                {
                    if (!TryParseMinorAndPatch(parts[1], out minor, out patch))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TryParseTwoDigitNumber(parts[1], out minor))
                    {
                        return false;
                    }

                    if (!TryParsePatchToken(parts[2], out patch))
                    {
                        return false;
                    }
                }

                normalized = $"{major}{minor:00}{patch:00}";
                return normalized.Length == ClientVersionLength;
            }

            return false;
        }

        private static bool TryParseSingleDigit(string token, out int value)
        {
            value = 0;
            foreach (char ch in token)
            {
                if (char.IsDigit(ch))
                {
                    value = ch - '0';
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseTwoDigitNumber(string token, out int value)
        {
            value = 0;
            int digits = 0;
            foreach (char ch in token)
            {
                if (!char.IsDigit(ch))
                {
                    break;
                }

                value = (value * 10) + (ch - '0');
                digits++;
                if (digits == 2)
                {
                    break;
                }
            }

            return digits > 0;
        }

        private static bool TryParseMinorAndPatch(string token, out int minor, out int patch)
        {
            minor = 0;
            patch = 0;
            int digitCount = 0;
            int patchDigits = 0;
            int patchDigitCount = 0;
            char letter = '\0';

            foreach (char ch in token)
            {
                if (char.IsDigit(ch))
                {
                    if (digitCount < 2)
                    {
                        minor = (minor * 10) + (ch - '0');
                    }
                    else if (patchDigitCount < 2)
                    {
                        patchDigits = (patchDigits * 10) + (ch - '0');
                        patchDigitCount++;
                    }

                    digitCount++;
                }
                else if (letter == '\0' && char.IsLetter(ch))
                {
                    letter = ch;
                }
            }

            if (digitCount == 0)
            {
                return false;
            }

            if (letter != '\0')
            {
                patch = LetterToNumber(letter);
                return patch > 0;
            }

            if (patchDigitCount > 0)
            {
                patch = patchDigits;
                return true;
            }

            patch = 0;
            return true;
        }

        private static bool TryParsePatchToken(string token, out int patch)
        {
            patch = 0;
            int digits = 0;
            foreach (char ch in token)
            {
                if (char.IsDigit(ch))
                {
                    if (digits < 2)
                    {
                        patch = (patch * 10) + (ch - '0');
                        digits++;
                    }
                }
                else if (char.IsLetter(ch))
                {
                    patch = LetterToNumber(ch);
                    return patch > 0;
                }
            }

            return digits > 0;
        }

        private static int LetterToNumber(char letter)
        {
            if (!char.IsLetter(letter))
            {
                return 0;
            }

            char lower = char.ToLowerInvariant(letter);
            if (lower < 'a' || lower > 'z')
            {
                return 0;
            }

            return lower - 'a' + 1;
        }

        private static byte[] ToFixedLengthAsciiBytes(string value, int length)
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            if (bytes.Length == length)
            {
                return bytes;
            }

            var result = new byte[length];
            Array.Copy(bytes, result, Math.Min(bytes.Length, length));
            return result;
        }

        // IAsyncDisposable Implementation
        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing NetworkManager...");
            _managerCts.Cancel();
            _pingSender.Dispose();
            await _connectionManager.DisposeAsync();
            _managerCts.Dispose();
            _logger.LogInformation("NetworkManager disposed.");
            GC.SuppressFinalize(this);
        }
    }
}
