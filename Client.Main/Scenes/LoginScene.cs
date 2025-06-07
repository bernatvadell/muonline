using Client.Main.Controllers; // Needed for SoundController
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Login;
using Client.Main.Core.Client;
using Client.Main.Core.Models;   // Add using for ServerInfo
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets; // Needed for CharacterClassNumber
using MUnique.OpenMU.Network.Packets.ServerToClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoginScene : BaseScene
    {
        // Fields
        private LoginDialog _loginDialog;
        private ServerGroupSelector _nonEventGroup;
        private ServerGroupSelector _eventGroup;
        private ServerList _serverList;
        private LabelControl _statusLabel;
        private ClientConnectionState _previousStateHandled = ClientConnectionState.Initial;

        private NetworkManager _networkManager;
        private ILogger<LoginScene> _logger;
        private bool _uiInitialized = false; // Flag for one-time UI initialization

        // Constructors
        public LoginScene()
        {
            _logger = MuGame.AppLoggerFactory?.CreateLogger<LoginScene>() ??
                      throw new InvalidOperationException("LoggerFactory not initialized in MuGame");
            _logger.LogInformation("LoginScene constructor called.");

            _statusLabel = new LabelControl
            {
                Text = "Status: Initializing...",
                X = 10,
                Y = 10,
                FontSize = 14,
                TextColor = Color.Yellow,
                Visible = true // Status label usually always visible
            };
            Controls.Add(_statusLabel);

            _loginDialog = new LoginDialog()
            {
                Visible = false, // Will be set by HandleConnectionStateChange
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter
            };
            _loginDialog.LoginAttempt += LoginDialog_LoginAttempt;
            Controls.Add(_loginDialog);

            // Server selection UI elements are initialized later in InitializeServerSelectionUI
            _nonEventGroup = null;
            _eventGroup = null;
            _serverList = null;
        }

        // This method is now part of the new progress reporting system
        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            // Overall LoginScene progress: 0.0 to 1.0
            // Let's say Network Init is 0% to 20% (0.0 to 0.2 progress)
            // World Loading is 20% to 70% (0.2 to 0.7 progress)
            // Sound & UI checks are 70% to 90% (0.7 to 0.9 progress)

            progressCallback?.Invoke("Initializing Network Manager...", 0.05f);
            _networkManager = MuGame.Network ?? throw new InvalidOperationException("NetworkManager not initialized in MuGame");
            SubscribeToNetworkEvents();
            progressCallback?.Invoke("Network Manager Ready.", 0.20f);

            progressCallback?.Invoke("Loading Login World...", 0.21f);

            World?.Dispose();
            var loginWorld = new NewLoginWorld();
            Controls.Add(loginWorld);
            // Assuming NewLoginWorld.Initialize() is relatively fast.
            // If it were slow, it would need its own progress reporting that this method would scale.
            await loginWorld.Initialize();
            World = loginWorld;

            progressCallback?.Invoke("Login World Loaded.", 0.70f);

            if (loginWorld.Terrain != null)
            {
                loginWorld.Terrain.AmbientLight = 0.2f;
            }

            progressCallback?.Invoke("Playing Login Theme...", 0.75f);
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");

            UpdateStatusLabel(_networkManager.CurrentState);

            progressCallback?.Invoke("Checking Connection State...", 0.80f);
            if (_networkManager.CurrentState >= ClientConnectionState.ConnectedToConnectServer)
            {
                _logger.LogInformation("LoginScene loaded, NetworkManager already connected or connecting. State: {State}", _networkManager.CurrentState);
                HandleConnectionStateChange(this, _networkManager.CurrentState);
                if (!_uiInitialized && _networkManager.CurrentState >= ClientConnectionState.ReceivedServerList)
                {
                    InitializeServerSelectionUI(); // This will add controls, they'll be initialized by BaseScene's main loop if not already
                }
            }
            else if (_networkManager.CurrentState == ClientConnectionState.Initial ||
                     _networkManager.CurrentState == ClientConnectionState.Disconnected)
            {
                _logger.LogInformation("LoginScene loaded, initiating connection to Connect Server...");
                _ = _networkManager.ConnectToConnectServerAsync(); // Fire and forget
            }
            progressCallback?.Invoke("Login Scene Setup Complete.", 0.90f);
        }

        // Public Methods
        public override Task Load()
        {
            // This will be called by base.InitializeWithProgressReporting if LoadSceneContentWithProgress isn't overridden
            // or if super.Load() is called. For our new structure, direct work is in LoadSceneContentWithProgress.
            _logger.LogDebug("LoginScene.Load() was called (likely by base). Main work in LoadSceneContentWithProgress.");
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _logger?.LogDebug("Disposing LoginScene, unsubscribing from network events.");
            UnsubscribeFromNetworkEvents();
            base.Dispose();
        }

        // Private Methods
        private void InitializeServerSelectionUI()
        {
            if (_uiInitialized) return;
            _logger.LogInformation("Initializing Server Selection UI...");

            _nonEventGroup = new ServerGroupSelector(false)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Left = -220 },
                Visible = true // Initial visibility handled by HandleConnectionStateChange
            };
            for (byte i = 0; i < 1; i++) _nonEventGroup.AddServer(i, $"Servers");

            _eventGroup = new ServerGroupSelector(true)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Right = -220 },
                Visible = true // Initial visibility handled by HandleConnectionStateChange
            };
            for (byte i = 0; i < 1; i++) _eventGroup.AddServer(i, $"Events");

            _serverList = new ServerList
            {
                Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter,
                Visible = false // Initial visibility handled by HandleConnectionStateChange
            };

            _nonEventGroup.SelectedIndexChanged += NonEventGroup_SelectedIndexChanged;
            _eventGroup.SelectedIndexChanged += EventGroup_SelectedIndexChanged;
            _serverList.ServerClick += ServerList_ServerClick;

            Controls.Add(_nonEventGroup);
            Controls.Add(_eventGroup);
            Controls.Add(_serverList);

            _uiInitialized = true;
            _logger.LogInformation("Server Selection UI Initialized and added to scene controls.");
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _networkManager.ServerListReceived += HandleServerListReceived;
                _networkManager.CharacterListReceived += HandleCharacterListReceived;
                _networkManager.LoginSuccess += HandleLoginSuccess;
                _networkManager.LoginFailed += HandleLoginFailed;
                _networkManager.ErrorOccurred += HandleNetworkError;
                _logger.LogDebug("Subscribed to NetworkManager events.");
            }
            else
            {
                _logger.LogError("Cannot subscribe to NetworkManager events: NetworkManager is null.");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _networkManager.ServerListReceived -= HandleServerListReceived;
                _networkManager.CharacterListReceived -= HandleCharacterListReceived;
                _networkManager.LoginSuccess -= HandleLoginSuccess;
                _networkManager.LoginFailed -= HandleLoginFailed;
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _logger.LogDebug("Unsubscribed from NetworkManager events.");
            }
        }

        // --- Network Event Handlers ---
        private void HandleConnectionStateChange(object sender, ClientConnectionState newState)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogDebug(">>> HandleConnectionStateChange (UI Thread): Received state {NewState} (Previous handled: {PrevState})", newState, _previousStateHandled);
                UpdateStatusLabel(newState);

                bool showServerSelectionUi = false;
                bool showLoginDialog = false;
                bool hideAll = false;
                bool showErrorAndExit = false;

                switch (newState)
                {
                    case ClientConnectionState.ConnectedToConnectServer:
                    case ClientConnectionState.RequestingServerList:
                    case ClientConnectionState.ReceivedServerList:
                        if (!_uiInitialized) InitializeServerSelectionUI();
                        showServerSelectionUi = true;
                        _logger.LogDebug("HandleConnectionStateChange: Setting showServerSelectionUi = true");
                        break;
                    case ClientConnectionState.RequestingConnectionInfo:
                    case ClientConnectionState.ReceivedConnectionInfo:
                    case ClientConnectionState.ConnectingToGameServer:
                        hideAll = true;
                        _logger.LogDebug("HandleConnectionStateChange: Setting hideAll = true");
                        break;
                    case ClientConnectionState.ConnectedToGameServer:
                        showLoginDialog = true;
                        _loginDialog?.FocusUsername();
                        _logger.LogDebug("HandleConnectionStateChange: Setting showLoginDialog = true and focusing username.");
                        break;
                    case ClientConnectionState.Authenticating:
                        showLoginDialog = true;
                        _logger.LogDebug("HandleConnectionStateChange: Setting showLoginDialog = true (Authenticating)");
                        break;
                    case ClientConnectionState.SelectingCharacter:
                    case ClientConnectionState.InGame:
                        hideAll = true;
                        _logger.LogDebug("HandleConnectionStateChange: Setting hideAll = true (Post-Login/SelectingCharacter)");
                        break;
                    case ClientConnectionState.Disconnected:
                    case ClientConnectionState.Initial:
                        ResetServerSelectionUI();
                        hideAll = true;
                        if (_previousStateHandled >= ClientConnectionState.ConnectingToConnectServer && _previousStateHandled < ClientConnectionState.InGame)
                        {
                            showErrorAndExit = true;
                        }
                        _logger.LogDebug("HandleConnectionStateChange: Setting hideAll = true (Disconnected/Initial). ShowError: {ShowError}", showErrorAndExit);
                        break;
                }

                UpdateUIVisibility(showServerSelectionUi, showLoginDialog, hideAll);

                if (showErrorAndExit)
                {
                    if (!Controls.OfType<MessageWindow>().Any(mw => mw.Text.Contains("Connection lost")))
                    {
                        MessageWindow msg = MessageWindow.Show("Connection lost to the server.");
                        if (msg != null)
                        {
                            msg.Closed += (s, e) =>
                            {
                                _logger.LogInformation("Closing game after connection lost message.");
                                MuGame.ScheduleOnMainThread(() => MuGame.Instance.Exit());
                            };
                        }
                        else
                        {
                            _logger.LogError("Failed to show MessageWindow for connection lost. Exiting game directly.");
                            MuGame.ScheduleOnMainThread(() => MuGame.Instance.Exit());
                        }
                    }
                }

                _previousStateHandled = newState;
                _logger.LogDebug("<<< HandleConnectionStateChange (UI Thread): Finished applying visibility. State handled: {NewState}", newState);
            });
        }

        private void UpdateUIVisibility(bool showServerSelectionUi, bool showLoginDialog, bool hideAll)
        {
            if (_nonEventGroup != null) _nonEventGroup.Visible = showServerSelectionUi && !hideAll;
            if (_eventGroup != null) _eventGroup.Visible = showServerSelectionUi && !hideAll;
            if (_loginDialog != null) _loginDialog.Visible = showLoginDialog && !hideAll;

            if (_serverList != null)
            {
                bool groupSelected = (_nonEventGroup?.ActiveIndex != -1) || (_eventGroup?.ActiveIndex != -1);
                _serverList.Visible = showServerSelectionUi && groupSelected &&
                                      _serverList.Controls.Count > 0 && !hideAll;
            }
        }

        private void ResetServerSelectionUI()
        {
            if (_uiInitialized)
            {
                if (_nonEventGroup != null) Controls.Remove(_nonEventGroup);
                if (_eventGroup != null) Controls.Remove(_eventGroup);
                if (_serverList != null) Controls.Remove(_serverList);
                _nonEventGroup = null;
                _eventGroup = null;
                _serverList = null;
                _uiInitialized = false;
                _logger.LogDebug("HandleConnectionStateChange: Resetting Server Selection UI.");
            }
        }

        private void LoginDialog_LoginAttempt(object sender, EventArgs e)
        {
            if (_networkManager.CurrentState == ClientConnectionState.ConnectedToGameServer ||
                _networkManager.CurrentState == ClientConnectionState.Authenticating)
            {
                string username = _loginDialog.Username;
                string password = _loginDialog.Password;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageWindow.Show("Please enter Username and Password.");
                    return;
                }
                _logger.LogInformation("Login attempt from dialog for user: {Username}", username);
                _ = _networkManager.SendLoginRequestAsync(username, password);
            }
            else
            {
                _logger.LogWarning("Login attempt ignored, invalid state: {State}", _networkManager.CurrentState);
                MessageWindow.Show($"Cannot login in state: {_networkManager.CurrentState}");
            }
        }

        private void HandleServerListReceived(object sender, List<ServerInfo> servers)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("UI processing ServerListReceived: {Count} servers", servers.Count);
                if (!_uiInitialized)
                {
                    InitializeServerSelectionUI();
                }
                // Visibility is now handled by HandleConnectionStateChange after state update
            });
        }

        private void HandleCharacterListReceived(object sender,
            List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_networkManager.CurrentState < ClientConnectionState.ConnectedToGameServer)
                {
                    _logger.LogWarning("HandleCharacterListReceived (UI Thread): Received character list but current state is {State}. Ignoring.", _networkManager.CurrentState);
                    return;
                }

                _logger.LogInformation(">>> HandleCharacterListReceived (UI Thread) for {Count} characters. Current State: {State}",
                    characters?.Count ?? 0, _networkManager.CurrentState);

                if (MuGame.Instance.ActiveScene != this)
                {
                    _logger.LogWarning("HandleCharacterListReceived (UI Thread): Scene changed. Aborting.");
                    return;
                }

                if (characters != null)
                {
                    try
                    {
                        _logger.LogInformation("--- Creating SelectCharacterScene instance...");
                        var newScene = new SelectCharacterScene(characters, _networkManager);
                        _logger.LogInformation("--- SelectCharacterScene instance created.");
                        _logger.LogInformation("--- Calling ChangeScene...");
                        MuGame.Instance.ChangeScene(newScene);
                        _logger.LogInformation("--- ChangeScene call finished (async void).");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "!!! Exception DURING scene creation or ChangeScene call.");
                        MessageWindow.Show("Error preparing character selection scene.");
                        _networkManager?.UpdateState(ClientConnectionState.ConnectedToGameServer);
                    }
                }
                else
                {
                    _logger.LogError("<<< Received null character list reference.");
                    MessageWindow.Show("Error receiving character list data.");
                    _networkManager?.UpdateState(ClientConnectionState.ConnectedToGameServer);
                }
                _logger.LogInformation("<<< HandleCharacterListReceived (UI Thread) finished.");
            });
        }

        private void HandleLoginSuccess(object sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("UI received LoginSuccess.");
                if (_statusLabel != null) _statusLabel.Text = "Status: Logged In - Requesting Characters...";
            });
        }

        private void HandleLoginFailed(object sender, LoginResponse.LoginResult reason)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogWarning("UI received LoginFailed. Reason: {Reason} (Value: {ReasonByte})", reason, (byte)reason);
                if (_statusLabel != null) _statusLabel.Text = "Status: Login Failed!";

                string messageToShow = reason == LoginResponse.LoginResult.AccountAlreadyConnected
                    ? "Account is already connected."
                    : "Login Failed. Check credentials or server status.";
                MessageWindow.Show(messageToShow);
                // Visibility of login dialog, etc., is handled by HandleConnectionStateChange after state update
            });
        }

        private void HandleNetworkError(object sender, string errorMessage)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogError("UI received NetworkError: {Error}", errorMessage);
                MessageWindow.Show($"Network Error: {errorMessage}");
                UpdateStatusLabel(ClientConnectionState.Disconnected); // This will trigger UI updates via HandleConnectionStateChange
            });
        }

        // --- UI Event Handlers ---
        private void NonEventGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_nonEventGroup == null || _serverList == null || _eventGroup == null)
                {
                    _logger.LogWarning("NonEventGroup_SelectedIndexChanged: UI controls are null.");
                    return;
                }

                if (_nonEventGroup.ActiveIndex != -1)
                {
                    _logger.LogInformation("Non-Event Group selected: {Index}", _nonEventGroup.ActiveIndex);
                    _eventGroup.UnselectServer();

                    var currentServerList = _networkManager.GetCachedServerList();
                    _serverList.Clear();
                    foreach (var server in currentServerList)
                    {
                        // TODO: Filter servers for this group
                        _serverList.AddServer((byte)server.ServerId, $"Server {server.ServerId}", server.LoadPercentage);
                    }
                    _serverList.Visible = true;
                    if (_loginDialog != null) _loginDialog.Visible = false;
                }
                else
                {
                    _serverList.Clear();
                    _serverList.Visible = false;
                }
            });
        }

        private void EventGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_eventGroup == null || _serverList == null || _nonEventGroup == null)
                {
                    _logger.LogWarning("EventGroup_SelectedIndexChanged: UI controls are null.");
                    return;
                }

                if (_eventGroup.ActiveIndex != -1)
                {
                    _logger.LogInformation("Event Group selected: {Index}", _eventGroup.ActiveIndex);
                    _nonEventGroup.UnselectServer();

                    var currentServerList = _networkManager.GetCachedServerList();
                    _serverList.Clear();
                    foreach (var server in currentServerList)
                    {
                        // TODO: Filter event servers
                        _serverList.AddServer((byte)server.ServerId, $"Event Srv {server.ServerId}", server.LoadPercentage);
                    }
                    _serverList.Visible = true;
                    if (_loginDialog != null) _loginDialog.Visible = false;
                }
                else
                {
                    _serverList.Clear();
                    _serverList.Visible = false;
                }
            });
        }

        private void ServerList_ServerClick(object sender, ServerSelectEventArgs e)
        {
            _logger.LogInformation("Server selected: ID={ServerId}, Name={ServerName}. Requesting connection info...",
                e.Index, e.Name);
            if (_loginDialog != null) _loginDialog.ServerName = e.Name;

            // UI visibility will be handled by HandleConnectionStateChange when state changes to RequestingConnectionInfo
            _ = _networkManager.RequestGameServerConnectionAsync(e.Index);
        }

        // --- Helper Methods ---
        private void UpdateStatusLabel(ClientConnectionState state)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_statusLabel != null) _statusLabel.Text = $"Status: {state}";
            });
        }
    }
}