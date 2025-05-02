using Client.Main.Client;
using Client.Main.Controllers; // Potrzebne dla SoundController
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Login;
using Client.Main.Core.Models;   // Dodaj using dla ServerInfo
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets; // Potrzebne dla CharacterClassNumber
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoginScene : BaseScene
    {
        private LoginDialog _loginDialog;
        private ServerGroupSelector? _nonEventGroup; // Inicjalizuj jako null
        private ServerGroupSelector? _eventGroup;  // Inicjalizuj jako null
        private ServerList? _serverList;         // Inicjalizuj jako null
        private LabelControl _statusLabel; // Do wyświetlania statusu połączenia

        private NetworkManager _networkManager;
        private ILogger<LoginScene> _logger;
        private bool _uiInitialized = false; // Flaga do jednorazowej inicjalizacji UI

        public LoginScene()
        {
            _logger = MuGame.AppLoggerFactory?.CreateLogger<LoginScene>() ?? throw new InvalidOperationException("LoggerFactory not initialized in MuGame");
            _logger.LogInformation("LoginScene constructor called.");

            _statusLabel = new LabelControl { /* ... ustawienia ... */ };
            Controls.Add(_statusLabel);

            _loginDialog = new LoginDialog() { Visible = false, Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter };
            _loginDialog.LoginAttempt += LoginDialog_LoginAttempt; // **** PODEPNIJ NOWY EVENT ****
            Controls.Add(_loginDialog);

            // Kontrolki wyboru serwera/grupy zostaną utworzone później w InitializeServerSelectionUI()
            _nonEventGroup = null;
            _eventGroup = null;
            _serverList = null;
        }

        public override async Task Load()
        {
            _logger.LogInformation("LoginScene Load starting...");
            _networkManager = MuGame.Network ?? throw new InvalidOperationException("NetworkManager not initialized in MuGame");
            SubscribeToNetworkEvents(); // Subskrybuj PRZED połączeniem
            await ChangeWorldAsync<NewLoginWorld>();
            await base.Load();
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");
            UpdateStatusLabel(_networkManager.CurrentState);

            // Logika połączenia i UI
            if (_networkManager.CurrentState >= ClientConnectionState.ConnectedToConnectServer)
            {
                _logger.LogInformation("LoginScene loaded, NetworkManager already connected or connecting. State: {State}", _networkManager.CurrentState);
                HandleConnectionStateChange(this, _networkManager.CurrentState);
                if (!_uiInitialized && _networkManager.CurrentState >= ClientConnectionState.ReceivedServerList)
                {
                    InitializeServerSelectionUI();
                }
            }
            else if (_networkManager.CurrentState == ClientConnectionState.Initial || _networkManager.CurrentState == ClientConnectionState.Disconnected)
            {
                _logger.LogInformation("LoginScene loaded, initiating connection to Connect Server...");
                _ = _networkManager.ConnectToConnectServerAsync();
            }
            _logger.LogInformation("LoginScene Load finished.");
        }

        private void InitializeServerSelectionUI()
        {
            if (_uiInitialized) return;
            _logger.LogInformation("Initializing Server Selection UI...");

            _nonEventGroup = new ServerGroupSelector(false) { Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter, Margin = new Margin { Left = -220 }, Visible = true };
            for (byte i = 0; i < 1; i++) _nonEventGroup.AddServer(i, $"Servers");

            _eventGroup = new ServerGroupSelector(true) { Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter, Margin = new Margin { Right = -220 }, Visible = true };
            for (byte i = 0; i < 1; i++) _eventGroup.AddServer(i, $"Events");

            _serverList = new ServerList { Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter, Visible = false };

            _nonEventGroup.SelectedIndexChanged += NonEventGroup_SelectedIndexChanged;
            _eventGroup.SelectedIndexChanged += EventGroup_SelectedIndexChanged;
            _serverList.ServerClick += ServerList_ServerClick;

            Controls.Add(_nonEventGroup);
            Controls.Add(_eventGroup);
            Controls.Add(_serverList);

            _uiInitialized = true;
            _logger.LogInformation("Server Selection UI Initialized and added to scene controls.");
        }

        // Subskrypcja eventów NetworkManager
        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _networkManager.ServerListReceived += HandleServerListReceived;
                _networkManager.CharacterListReceived += HandleCharacterListReceived; // Ten jest potrzebny do zmiany sceny
                _networkManager.LoginSuccess += HandleLoginSuccess;
                _networkManager.LoginFailed += HandleLoginFailed;
                // _networkManager.EnteredGame -= HandleEnteredGame; // Upewnij się, że jest usunięty lub zakomentowany
                _networkManager.ErrorOccurred += HandleNetworkError;
                _logger.LogDebug("Subscribed to NetworkManager events.");
            }
            else { _logger.LogError("Cannot subscribe to NetworkManager events: NetworkManager is null."); }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _networkManager.ServerListReceived -= HandleServerListReceived;
                _networkManager.CharacterListReceived -= HandleCharacterListReceived; // Ten jest potrzebny
                _networkManager.LoginSuccess -= HandleLoginSuccess;
                _networkManager.LoginFailed -= HandleLoginFailed;
                // _networkManager.EnteredGame -= HandleEnteredGame; // Upewnij się, że jest usunięty lub zakomentowany
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _logger.LogDebug("Unsubscribed from NetworkManager events.");
            }
        }

        // --- Network Event Handlers ---

        private void HandleConnectionStateChange(object? sender, ClientConnectionState newState)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogDebug(">>> HandleConnectionStateChange (UI Thread): Received state {NewState}", newState);
                UpdateStatusLabel(newState);

                bool showServerSelectionUi = false;
                bool showLoginDialog = false;
                bool hideAll = false;

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
                    case ClientConnectionState.SelectingCharacter: // Nadal pokazuj dialog logowania? Raczej ukryj wszystko.
                    case ClientConnectionState.InGame:
                        hideAll = true;
                        _logger.LogDebug("HandleConnectionStateChange: Setting hideAll = true (Post-Login)");
                        break;
                    case ClientConnectionState.Disconnected:
                    case ClientConnectionState.Initial:
                        if (_uiInitialized)
                        {
                            if (_nonEventGroup != null) Controls.Remove(_nonEventGroup);
                            if (_eventGroup != null) Controls.Remove(_eventGroup);
                            if (_serverList != null) Controls.Remove(_serverList);
                            _nonEventGroup = null; _eventGroup = null; _serverList = null;
                            _uiInitialized = false;
                            _logger.LogDebug("HandleConnectionStateChange: Resetting Server Selection UI.");
                        }
                        hideAll = true;
                        _logger.LogDebug("HandleConnectionStateChange: Setting hideAll = true (Disconnected/Initial)");
                        break;
                }

                _nonEventGroup?.SetVisible(showServerSelectionUi && !hideAll);
                _eventGroup?.SetVisible(showServerSelectionUi && !hideAll);
                _loginDialog?.SetVisible(showLoginDialog && !hideAll);

                if (_serverList != null)
                {
                    bool groupSelected = (_nonEventGroup?.ActiveIndex != -1) || (_eventGroup?.ActiveIndex != -1);
                    _serverList.Visible = showServerSelectionUi && groupSelected && _serverList.Controls.Count > 0 && !hideAll;
                    _logger.LogTrace("HandleConnectionStateChange: ServerList Visible = {IsVisible} (showUI={showUI}, groupSel={grpSel}, count={count}, hideAll={hide})", _serverList.Visible, showServerSelectionUi, groupSelected, _serverList.Controls.Count, hideAll);
                }
                _logger.LogDebug("<<< HandleConnectionStateChange (UI Thread): Finished applying visibility.");
            });
        }

        private void LoginDialog_LoginAttempt(object? sender, EventArgs e)
        {
            if (_networkManager.CurrentState == ClientConnectionState.ConnectedToGameServer || _networkManager.CurrentState == ClientConnectionState.Authenticating)
            {
                string username = _loginDialog.Username;
                string password = _loginDialog.Password;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) { MessageWindow.Show("Please enter Username and Password."); return; }
                _logger.LogInformation("Login attempt from dialog for user: {Username}", username);
                _ = _networkManager.SendLoginRequestAsync(username, password);
            }
            else
            {
                _logger.LogWarning("Login attempt ignored, invalid state: {State}", _networkManager.CurrentState);
                MessageWindow.Show($"Cannot login in state: {_networkManager.CurrentState}");
            }
        }

        // Handler otrzymania listy serwerów
        private void HandleServerListReceived(object? sender, List<ServerInfo> servers)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("UI processing ServerListReceived: {Count} servers", servers.Count);
                if (!_uiInitialized) { InitializeServerSelectionUI(); }
                else
                {
                    if (_nonEventGroup != null) _nonEventGroup.Visible = true;
                    if (_eventGroup != null) _eventGroup.Visible = true;
                    if (_serverList != null) _serverList.Visible = false;
                    _loginDialog.Visible = false;
                }
            });
        }

        // Handler otrzymania listy postaci
        private void HandleCharacterListReceived(object? sender, List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation(">>> HandleCharacterListReceived (UI Thread) starting execution for {Count} characters.", characters?.Count ?? 0);
                if (MuGame.Instance.ActiveScene != this) { _logger.LogWarning("HandleCharacterListReceived (UI Thread): Scene changed before execution. Aborting."); return; }

                if (characters != null) // Check if the list is not null
                {
                    SelectCharacterScene? newScene = null;
                    try
                    {
                        _logger.LogInformation("--- HandleCharacterListReceived (UI Thread): Attempting to create SelectCharacterScene instance...");
                        // *** The 'characters' variable now has the correct type ***
                        newScene = new SelectCharacterScene(characters);
                        _logger.LogInformation("--- HandleCharacterListReceived (UI Thread): SelectCharacterScene instance created successfully.");

                        _logger.LogInformation("--- HandleCharacterListReceived (UI Thread): Preparing to call ChangeScene...");
                        MuGame.Instance.ChangeScene(newScene); // Call ChangeScene with the new scene instance
                        _logger.LogInformation("--- HandleCharacterListReceived (UI Thread): MuGame.Instance.ChangeScene call finished (async void, execution continues).");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "!!! HandleCharacterListReceived (UI Thread): Exception DURING scene creation or ChangeScene call.");
                        MessageWindow.Show("Error preparing character selection scene.");
                        // Reset state if something went wrong during scene change
                        if (_networkManager != null) _networkManager.UpdateState(ClientConnectionState.ConnectedToGameServer);
                    }
                }
                else // Handle the case where the list itself is null (though NetworkManager sends an empty list now)
                {
                    _logger.LogError("<<< HandleCharacterListReceived (UI Thread): Received null character list reference.");
                    MessageWindow.Show("Error receiving character list data.");
                    if (_networkManager != null) _networkManager.UpdateState(ClientConnectionState.ConnectedToGameServer);
                }
                _logger.LogInformation("<<< HandleCharacterListReceived (UI Thread) finished execution.");
            });
        }


        private void HandleLoginSuccess(object? sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("UI received LoginSuccess.");
                if (_statusLabel != null) _statusLabel.Text = "Status: Logged In - Requesting Characters...";
            });
        }

        private void HandleLoginFailed(object? sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogWarning("UI received LoginFailed.");
                if (_statusLabel != null) _statusLabel.Text = "Status: Login Failed!";
                MessageWindow.Show("Login Failed. Check credentials or server status.");
                if (_nonEventGroup != null) _nonEventGroup.Visible = false;
                if (_eventGroup != null) _eventGroup.Visible = false;
                if (_serverList != null) _serverList.Visible = false;
                _loginDialog.Visible = true;
            });
        }

        // Handler wejścia do gry (po wyborze postaci)
        private void HandleEnteredGame(object? sender, EventArgs e)
        {
            // Ten handler może być teraz wywołany z wątku sieciowego!
            _logger.LogInformation(">>> HandleEnteredGame: Event received. Scheduling scene change to GameScene on main thread...");
            MuGame.ScheduleOnMainThread(() => // Nadal używamy harmonogramu do zmiany sceny
            {
                _logger.LogInformation("--- HandleEnteredGame (UI Thread): Executing scheduled scene change...");
                if (MuGame.Instance.ActiveScene == this)
                {
                    try
                    {
                        MuGame.Instance.ChangeScene<GameScene>();
                        _logger.LogInformation("<<< HandleEnteredGame (UI Thread): ChangeScene to GameScene call completed.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "!!! HandleEnteredGame (UI Thread): Exception during ChangeScene to GameScene.");
                    }
                }
                else
                {
                    _logger.LogWarning("<<< HandleEnteredGame (UI Thread): Scene changed before execution. Aborting change to GameScene.");
                }
            });
        }

        // Handler błędów sieciowych
        private void HandleNetworkError(object? sender, string errorMessage)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogError("UI received NetworkError: {Error}", errorMessage);
                MessageWindow.Show($"Network Error: {errorMessage}");
                UpdateStatusLabel(ClientConnectionState.Disconnected);

                // Ukryj UI wyboru serwera/login
                if (_nonEventGroup != null) _nonEventGroup.Visible = false;
                if (_eventGroup != null) _eventGroup.Visible = false;
                if (_serverList != null) _serverList.Visible = false;
                _loginDialog.Visible = false;
                _uiInitialized = false; // Zresetuj flagę UI po błędzie/rozłączeniu
            });
        }

        // --- UI Event Handlers ---

        // Handler kliknięcia grupy "Non-Event"
        private void NonEventGroup_SelectedIndexChanged(object? sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                // Sprawdzenia null dla bezpieczeństwa
                if (_nonEventGroup == null || _serverList == null || _eventGroup == null)
                {
                    _logger.LogWarning("NonEventGroup_SelectedIndexChanged called but UI controls are null.");
                    return;
                }

                if (_nonEventGroup.ActiveIndex != -1)
                {
                    _logger.LogInformation("Non-Event Group selected: {Index}", _nonEventGroup.ActiveIndex);
                    _eventGroup.UnselectServer(); // Odznacz drugą grupę

                    var currentServerList = _networkManager.GetCachedServerList();
                    _serverList.Clear();
                    foreach (var server in currentServerList)
                    {
                        // TODO: Filtrowanie serwerów dla tej grupy
                        _serverList.AddServer((byte)server.ServerId, $"Server {server.ServerId}", server.LoadPercentage);
                    }
                    _serverList.Visible = true;
                    _loginDialog.Visible = false;
                }
                else // Jeśli grupa została odznaczona
                {
                    _serverList.Clear();
                    _serverList.Visible = false;
                }
            });
        }

        // Handler kliknięcia grupy "Event"
        private void EventGroup_SelectedIndexChanged(object? sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_eventGroup == null || _serverList == null || _nonEventGroup == null)
                {
                    _logger.LogWarning("EventGroup_SelectedIndexChanged called but UI controls are null.");
                    return;
                }

                if (_eventGroup.ActiveIndex != -1)
                {
                    _logger.LogInformation("Event Group selected: {Index}", _eventGroup.ActiveIndex);
                    _nonEventGroup.UnselectServer(); // Odznacz drugą grupę

                    var currentServerList = _networkManager.GetCachedServerList();
                    _serverList.Clear();
                    foreach (var server in currentServerList)
                    {
                        // TODO: Filtrowanie serwerów eventowych
                        _serverList.AddServer((byte)server.ServerId, $"Event Srv {server.ServerId}", server.LoadPercentage);
                    }
                    _serverList.Visible = true;
                    _loginDialog.Visible = false;
                }
                else
                {
                    _serverList.Clear();
                    _serverList.Visible = false;
                }
            });
        }

        // Handler kliknięcia na konkretny serwer z listy
        private void ServerList_ServerClick(object? sender, ServerSelectEventArgs e)
        {
            _logger.LogInformation("Server selected: ID={ServerId}, Name={ServerName}. Requesting connection info...", e.Index, e.Name);
            _loginDialog.ServerName = e.Name; // Ustaw nazwę w dialogu logowania

            // Ukryj UI wyboru serwera/grupy
            MuGame.ScheduleOnMainThread(() => // Użyj harmonogramu dla operacji UI
            {
                if (_nonEventGroup != null) _nonEventGroup.Visible = false;
                if (_eventGroup != null) _eventGroup.Visible = false;
                if (_serverList != null) _serverList.Visible = false;
                _loginDialog.Visible = false; // Ukryj też dialog na czas łączenia
            });

            // Wyślij żądanie informacji o połączeniu do wybranego serwera gry
            _ = _networkManager.RequestGameServerConnectionAsync(e.Index);
        }

        // --- Helper Methods ---

        // Aktualizacja etykiety statusu
        private void UpdateStatusLabel(ClientConnectionState state)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_statusLabel != null) // Sprawdź null, bo może być wywołane przed pełną inicjalizacją
                {
                    _statusLabel.Text = $"Status: {state}";
                }
            });
        }

        // Sprzątanie przy zamykaniu sceny
        public override void Dispose()
        {
            _logger?.LogDebug("Disposing LoginScene, unsubscribing from network events.");
            UnsubscribeFromNetworkEvents(); // Anuluj subskrypcje
            base.Dispose(); // Wywołaj bazowe Dispose
        }

        // --- Usunięte metody ---
        // LoginButton_Click - logika przeniesiona do NetworkManager/Dialog
        // TryConnect, OnConnect - logika przeniesiona do NetworkManager
        // PacketReceived, OnDiscconected - logika przeniesiona do NetworkManager/Handlers
    }
}