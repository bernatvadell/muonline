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
            // Pobierz instancję NetworkManager z MuGame
            _networkManager = MuGame.Network ?? throw new InvalidOperationException("NetworkManager not initialized in MuGame");

            // Zasubskrybuj eventy NetworkManager *przed* potencjalnym wywołaniem akcji
            SubscribeToNetworkEvents();

            // Załaduj świat dla sceny logowania
            await ChangeWorldAsync<NewLoginWorld>(); // Użyj świata z lepszą grafiką
            await base.Load(); // Załaduj bazowe elementy sceny

            // Odtwórz muzykę w tle
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");

            // Zaktualizuj etykietę statusu na podstawie bieżącego stanu sieci
            UpdateStatusLabel(_networkManager.CurrentState);

            // Sprawdź, czy już jesteśmy połączeni (np. powrót do sceny)
            if (_networkManager.CurrentState >= ClientConnectionState.ConnectedToConnectServer)
            {
                _logger.LogInformation("LoginScene loaded, NetworkManager already connected or connecting. State: {State}", _networkManager.CurrentState);
                // Upewnij się, że UI jest odpowiednie dla bieżącego stanu
                HandleConnectionStateChange(this, _networkManager.CurrentState); // Wywołaj handler, aby zsynchronizować UI
                // Jeśli otrzymaliśmy już listę serwerów, ale UI nie było gotowe, zainicjuj je
                if (!_uiInitialized && _networkManager.CurrentState >= ClientConnectionState.ReceivedServerList)
                {
                    InitializeServerSelectionUI();
                }
            }
            // Jeśli nie jesteśmy połączeni, spróbuj nawiązać połączenie
            else if (_networkManager.CurrentState == ClientConnectionState.Initial || _networkManager.CurrentState == ClientConnectionState.Disconnected)
            {
                _logger.LogInformation("LoginScene loaded, initiating connection to Connect Server...");
                _ = _networkManager.ConnectToConnectServerAsync(); // Rozpocznij próbę połączenia w tle
            }
            _logger.LogInformation("LoginScene Load finished.");
        }

        // Inicjalizacja kontrolek UI wyboru serwera/grupy
        private void InitializeServerSelectionUI()
        {
            if (_uiInitialized) return; // Wykonaj tylko raz

            _logger.LogInformation("Initializing Server Selection UI...");

            _nonEventGroup = new ServerGroupSelector(false)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Left = -220 },
                Visible = true // Pokaż od razu po utworzeniu
            };
            // TODO: W przyszłości te grupy mogą pochodzić z konfiguracji lub Connect Servera
            for (byte i = 0; i < 1; i++) // Na razie statyczna grupa
                _nonEventGroup.AddServer(i, $"Servers");

            _eventGroup = new ServerGroupSelector(true)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Right = -220 },
                Visible = true // Pokaż od razu po utworzeniu
            };
            for (byte i = 0; i < 1; i++) // Na razie statyczna grupa
                _eventGroup.AddServer(i, $"Events");

            _serverList = new ServerList
            {
                Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter,
                Visible = false // Ukryta, aż grupa zostanie wybrana
            };

            // Podłącz handlery zdarzeń do nowo utworzonych kontrolek
            _nonEventGroup.SelectedIndexChanged += NonEventGroup_SelectedIndexChanged;
            _eventGroup.SelectedIndexChanged += EventGroup_SelectedIndexChanged;
            _serverList.ServerClick += ServerList_ServerClick;

            // Dodaj nowe kontrolki do kolekcji Controls sceny, aby były rysowane i aktualizowane
            Controls.Add(_nonEventGroup);
            Controls.Add(_eventGroup);
            Controls.Add(_serverList);

            _uiInitialized = true; // Ustaw flagę, że UI zostało zainicjowane
            _logger.LogInformation("Server Selection UI Initialized and added to scene controls.");
        }

        // Subskrypcja eventów NetworkManager
        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _networkManager.ServerListReceived += HandleServerListReceived;
                _networkManager.CharacterListReceived += HandleCharacterListReceived;
                _networkManager.LoginSuccess += HandleLoginSuccess;
                _networkManager.LoginFailed += HandleLoginFailed;
                _networkManager.EnteredGame += HandleEnteredGame;
                _networkManager.ErrorOccurred += HandleNetworkError;
                _logger.LogDebug("Subscribed to NetworkManager events.");
            }
            else
            {
                _logger.LogError("Cannot subscribe to NetworkManager events: NetworkManager is null.");
            }
        }

        // Anulowanie subskrypcji eventów NetworkManager
        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _networkManager.ServerListReceived -= HandleServerListReceived;
                _networkManager.CharacterListReceived -= HandleCharacterListReceived;
                _networkManager.LoginSuccess -= HandleLoginSuccess;
                _networkManager.LoginFailed -= HandleLoginFailed;
                _networkManager.EnteredGame -= HandleEnteredGame;
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _logger.LogDebug("Unsubscribed from NetworkManager events.");
            }
        }

        // --- Network Event Handlers ---

        // Handler zmiany stanu połączenia
        private void HandleConnectionStateChange(object? sender, ClientConnectionState newState)
        {
            // Użyj harmonogramu, aby upewnić się, że operacje na UI są wykonywane w głównym wątku gry
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogDebug("UI received ConnectionStateChanged: {NewState}", newState);
                UpdateStatusLabel(newState); // Zaktualizuj etykietę statusu

                // Domyślne wartości widoczności kontrolek
                bool showServerSelectionUi = false;
                bool showLoginDialog = false;

                // Ustaw widoczność kontrolek w zależności od nowego stanu sieciowego
                switch (newState)
                {
                    case ClientConnectionState.ConnectedToConnectServer:
                    case ClientConnectionState.ReceivedServerList:
                        // Jeśli jesteśmy połączeni z Connect Serverem lub otrzymaliśmy listę serwerów,
                        // pokaż UI wyboru grupy/serwera.
                        if (!_uiInitialized)
                        {
                            // Zainicjuj UI wyboru serwera, jeśli to pierwszy raz
                            InitializeServerSelectionUI();
                        }
                        showServerSelectionUi = true;
                        break;

                    case ClientConnectionState.RequestingConnectionInfo:
                    case ClientConnectionState.ReceivedConnectionInfo:
                    case ClientConnectionState.ConnectingToGameServer:
                    case ClientConnectionState.ConnectedToGameServer:
                    case ClientConnectionState.Authenticating:
                        // W stanach związanych z łączeniem/logowaniem do Game Servera,
                        // pokaż dialog logowania.
                        showLoginDialog = true;
                        showServerSelectionUi = false; // Ukryj wybór serwera/grupy

                        // Jeśli właśnie przeszliśmy do stanu ConnectedToGameServer,
                        // ustaw fokus na polu nazwy użytkownika w dialogu logowania.
                        if (newState == ClientConnectionState.ConnectedToGameServer)
                        {
                            _loginDialog?.FocusUsername(); // Użyj ?. dla bezpieczeństwa
                        }
                        break;

                    case ClientConnectionState.InGame:
                        // Stan InGame jest obsługiwany przez zmianę sceny,
                        // więc tutaj nie musimy nic robić z UI tej sceny.
                        break;

                    case ClientConnectionState.Disconnected:
                    case ClientConnectionState.Initial: // Również dla stanu początkowego
                                                        // Po rozłączeniu lub w stanie początkowym, zresetuj UI.
                        _uiInitialized = false; // Zresetuj flagę inicjalizacji UI

                        // Usuń kontrolki wyboru serwera z kolekcji Controls sceny, jeśli istnieją
                        if (_nonEventGroup != null) Controls.Remove(_nonEventGroup);
                        if (_eventGroup != null) Controls.Remove(_eventGroup);
                        if (_serverList != null) Controls.Remove(_serverList);

                        // Ustaw referencje na null, aby GC mógł je zebrać
                        _nonEventGroup = null;
                        _eventGroup = null;
                        _serverList = null;

                        // Ukryj również dialog logowania
                        showLoginDialog = false;
                        showServerSelectionUi = false;
                        break;

                        // Obsługa innych stanów, jeśli zajdzie taka potrzeba
                        // case ClientConnectionState.SelectingCharacter:
                        //     // Można by np. zablokować dialog logowania
                        //     break;
                }

                // Zastosuj obliczoną widoczność do kontrolek UI
                // Używaj operatora ?. (null-conditional), aby uniknąć błędów, jeśli kontrolki
                // zostały usunięte (np. po rozłączeniu).
                _nonEventGroup?.SetVisible(showServerSelectionUi);
                _eventGroup?.SetVisible(showServerSelectionUi);

                // Lista serwerów jest widoczna tylko, gdy UI wyboru jest aktywne ORAZ gdy lista ma elementy.
                // (Logika wypełniania listy jest w handlerach SelectedIndexChanged grup).
                if (_serverList != null)
                {
                    _serverList.Visible = _serverList.Controls.Count > 0 && showServerSelectionUi;
                }

                _loginDialog?.SetVisible(showLoginDialog); // Użyj metody SetVisible dla pewności
            });
        }

        // **** NOWY HANDLER DLA LOGIN DIALOG ****
        private void LoginDialog_LoginAttempt(object? sender, EventArgs e)
        {
            // Sprawdź, czy jesteśmy w odpowiednim stanie do logowania
            if (_networkManager.CurrentState == ClientConnectionState.ConnectedToGameServer)
            {
                string username = _loginDialog.Username;
                string password = _loginDialog.Password;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageWindow.Show("Please enter Username and Password.");
                    return;
                }

                _logger.LogInformation("Login attempt from dialog for user: {Username}", username);
                // Wywołaj nową metodę w NetworkManager, przekazując dane
                _ = _networkManager.SendLoginRequestAsync(username, password);
            }
            else
            {
                _logger.LogWarning("Login attempt ignored, invalid state: {State}", _networkManager.CurrentState);
            }
        }
        // **** KONIEC NOWEGO HANDLERA ****

        // Handler otrzymania listy serwerów
        private void HandleServerListReceived(object? sender, List<ServerInfo> servers)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("UI processing ServerListReceived: {Count} servers", servers.Count);

                // Zainicjuj UI wyboru serwera, jeśli jeszcze nie istnieje
                if (!_uiInitialized)
                {
                    InitializeServerSelectionUI();
                }
                else // Jeśli UI już istnieje, upewnij się, że grupy są widoczne
                {
                    if (_nonEventGroup != null) _nonEventGroup.Visible = true;
                    if (_eventGroup != null) _eventGroup.Visible = true;
                    if (_serverList != null) _serverList.Visible = false; // Ukryj listę serwerów
                    _loginDialog.Visible = false; // Ukryj dialog logowania
                }
                // Nie wypełniamy _serverList tutaj, tylko po kliknięciu grupy
            });
        }

        // Handler otrzymania listy postaci
        private void HandleCharacterListReceived(object? sender, List<(string Name, CharacterClassNumber Class)> characters)
        {
            _logger.LogInformation("Character list received ({Count}), waiting for selection (handled in SelectCharacterScene).", characters.Count);
            // Ta scena nie robi nic z listą postaci, tylko loguje
        }

        // Handler pomyślnego zalogowania (przed otrzymaniem listy postaci)
        private void HandleLoginSuccess(object? sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("UI received LoginSuccess.");
                if (_statusLabel != null) _statusLabel.Text = "Status: Logged In - Requesting Characters...";
                // Dialog logowania pozostaje widoczny
            });
        }

        // Handler nieudanego logowania
        private void HandleLoginFailed(object? sender, EventArgs e)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogWarning("UI received LoginFailed.");
                if (_statusLabel != null) _statusLabel.Text = "Status: Login Failed!";
                MessageWindow.Show("Login Failed. Check credentials or server status.");

                // Pokaż ponownie dialog logowania, ukryj wybór serwera
                if (_nonEventGroup != null) _nonEventGroup.Visible = false;
                if (_eventGroup != null) _eventGroup.Visible = false;
                if (_serverList != null) _serverList.Visible = false;
                _loginDialog.Visible = true;
            });
        }

        // Handler wejścia do gry (po wyborze postaci)
        private void HandleEnteredGame(object? sender, EventArgs e)
        {
            _logger.LogInformation("UI received EnteredGame. Changing scene...");
            // Użyj ScheduleOnMainThread do zmiany sceny
            MuGame.ScheduleOnMainThread(() =>
            {
                MuGame.Instance.ChangeScene<GameScene>();
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
            if (_nonEventGroup != null) _nonEventGroup.Visible = false;
            if (_eventGroup != null) _eventGroup.Visible = false;
            if (_serverList != null) _serverList.Visible = false;
            // Dialog logowania stanie się widoczny po zmianie stanu na ConnectingToGameServer lub Authenticating

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