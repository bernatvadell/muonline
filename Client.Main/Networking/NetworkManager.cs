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

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<NetworkManager> _logger;
        private readonly MuOnlineSettings _settings;
        private readonly ConnectionManager _connectionManager;
        private readonly PacketRouter _packetRouter;
        private readonly LoginService _loginService;
        private readonly CharacterService _characterService;
        private readonly ConnectServerService _connectServerService;
        private readonly CharacterState _characterState;
        private readonly ScopeManager _scopeManager;
        private readonly Dictionary<byte, byte> _serverDirectionMap;
        private string _selectedCharacterNameForLogin = string.Empty;

        private ClientConnectionState _currentState = ClientConnectionState.Initial;
        private List<ServerInfo> _serverList = new();
        private CancellationTokenSource _managerCts;

        // Events
        public event EventHandler<ClientConnectionState> ConnectionStateChanged;
        public event EventHandler<List<ServerInfo>> ServerListReceived;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<List<(string Name, CharacterClassNumber Class, ushort Level)>> CharacterListReceived;
        public event EventHandler LoginSuccess;
        public event EventHandler LoginFailed;
        public event EventHandler EnteredGame;

        // Properties
        public ClientConnectionState CurrentState => _currentState;
        public bool IsConnected => _connectionManager.IsConnected;
        public CharacterState GetCharacterState() => _characterState;

        // Constructors
        public NetworkManager(ILoggerFactory loggerFactory, MuOnlineSettings settings, CharacterState characterState, ScopeManager scopeManager)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<NetworkManager>();
            _settings = settings;
            _characterState = characterState;
            _scopeManager = scopeManager;

            var clientVersionBytes = Encoding.ASCII.GetBytes(settings.ClientVersion);
            var clientSerialBytes = Encoding.ASCII.GetBytes(settings.ClientSerial);
            var targetVersion = System.Enum.Parse<TargetProtocolVersion>(settings.ProtocolVersion, ignoreCase: true);

            _connectionManager = new ConnectionManager(loggerFactory, EncryptKeys, DecryptKeys);

            _loginService = new LoginService(_connectionManager, _loggerFactory.CreateLogger<LoginService>(), clientVersionBytes, clientSerialBytes, Xor3Keys);
            _characterService = new CharacterService(_connectionManager, _loggerFactory.CreateLogger<CharacterService>());
            _connectServerService = new ConnectServerService(_connectionManager, _loggerFactory.CreateLogger<ConnectServerService>());

            _packetRouter = new PacketRouter(loggerFactory, _characterService, _loginService, targetVersion, this, _characterState, _scopeManager, _settings);

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
            _logger.LogInformation("🕯  ProcessCharacterRespawn → map {Map}, pos ({X},{Y}), dir {Dir}",
                                   mapId, x, y, direction);

            _characterState.UpdateMap(mapId);
            _characterState.UpdatePosition(x, y);
            _characterState.UpdateDirection(direction); // Update direction in character state

            MuGame.ScheduleOnMainThread(async () =>
            {
                try
                {
                    var game = MuGame.Instance;
                    if (game?.ActiveScene is not GameScene gs)
                    {
                        // Not in game? Reload the scene.
                        game.ChangeScene(new GameScene());
                        return;
                    }

                    Type newWorldType = null;
                    if (!GameScene.MapWorldRegistry.TryGetValue((byte)mapId, out newWorldType))
                    {
                        _logger.LogError("ProcessCharacterRespawn: Unknown mapId {MapId} from server. Cannot change map.", mapId);
                        // Optionally, fall back to a default or disconnect. For now, let's assume it won't proceed.
                        return;
                    }

                    if (gs.World == null || gs.World.GetType() != newWorldType)
                    {
                        // Map changed - first set hero to spawn, then rebuild.
                        var hero = gs.Hero;
                        hero.Reset(); // Clears path and MoveTargetPosition
                        hero.Location = new Vector2(x, y);
                        hero.Direction = (Models.Direction)direction;
                        // MoveTargetPosition and Position will be set by ChangeMap after _hero.Location and _hero.Direction are updated
                        // from the latest CharacterState, which is now current.

                        _logger.LogDebug("Respawn changes map from {OldWorldType} to {NewWorldType}. Hero initial Location ({Hx},{Hy}), Direction {Hd}",
                                         gs.World?.GetType().Name ?? "null",
                                         newWorldType.Name,
                                         hero.Location.X, hero.Location.Y, hero.Direction);
                        await gs.ChangeMap(newWorldType);
                    }
                    // Removed else-if for GameScene.MapWorldRegistry.TryGetValue as it's handled above.
                    /* else
                        {
                            _logger.LogWarning("Unknown mapId {MapId} on respawn – new GameScene()", mapId);
                            game.ChangeScene(new GameScene());
                        }
                        return;
                    }
                    */
                    // Same map - local teleport.
                    else
                    {
                        var hero = gs.Hero;
                        hero.Reset();
                        hero.Location = new Vector2(x, y); // Explicitly update current hero's location
                        hero.Direction = (Models.Direction)direction; // And direction
                        hero.MoveTargetPosition = hero.TargetPosition;
                        hero.Position = hero.TargetPosition;
                        _logger.LogDebug("Hero teleported to ({X},{Y}) on map {Map}.", x, y, mapId);

                        _logger.LogInformation("Respawn on same map. Sending ClientReadyAfterMapChange.");
                        await _characterService.SendClientReadyAfterMapChangeAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 ProcessCharacterRespawn exception.");
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

        internal void ProcessCharacterList(List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
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
                string reason = result.ToString();
                _logger.LogError("Login failed: {Reason}", reason);
                OnErrorOccurred($"Login failed: {reason}");
                MuGame.ScheduleOnMainThread(() => LoginFailed?.Invoke(this, EventArgs.Empty));
                UpdateState(ClientConnectionState.ConnectedToGameServer); // Allow retry
            }
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
            _logger.LogInformation("<<< ProcessCharacterInformation: State updated and EnteredGame event raised.");
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

        // IAsyncDisposable Implementation
        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing NetworkManager...");
            _managerCts.Cancel();
            await _connectionManager.DisposeAsync();
            _managerCts.Dispose();
            _logger.LogInformation("NetworkManager disposed.");
            GC.SuppressFinalize(this);
        }
    }
}