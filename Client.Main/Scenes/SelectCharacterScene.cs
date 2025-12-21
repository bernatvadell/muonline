using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game;
using Client.Main.Controls.UI.SelectCharacter;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class SelectCharacterScene : BaseScene
    {
        private static class Theme
        {
            // Background layers
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            // Accent - Warm Gold
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            // Secondary accent - Cool Blue
            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

            // Borders
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            // Text
            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            // Status colors
            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private const int PANEL_WIDTH = 340;
        private const int PANEL_MARGIN = 30;
        private const int HEADER_HEIGHT = 45;
        private const int BUTTON_HEIGHT = 36;
        private const int BUTTON_SPACING = 8;
        private const int INNER_PADDING = 12;
        private const int CHAR_CARD_HEIGHT = 65;
        private const int CHAR_CARD_SPACING = 6;

        // Fields
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _characters;
        private SelectWorld _selectWorld;
        private readonly NetworkManager _networkManager;
        private ILogger<SelectCharacterScene> _logger;
        private (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)? _selectedCharacterInfo = null;
        private LoadingScreenControl _loadingScreen;
        private bool _initialLoadComplete = false;
        private ButtonControl _previousCharacterButton;
        private ButtonControl _nextCharacterButton;
        private int _currentCharacterIndex = -1;
        private bool _isSelectionInProgress = false;
        private Texture2D _backgroundTexture;
        private ProgressBarControl _progressBar;
        private bool _previousDayNightEnabled;
        private Vector3 _previousSunDirection;
        private bool _dayNightPatched;
        private ButtonControl _createCharacterButton;
        private ButtonControl _deleteCharacterButton;
        private ButtonControl _enterGameButton;
        private ButtonControl _exitButton;
        private CharacterCreationDialog _characterCreationDialog;
        private string _currentlySelectedCharacterName = null;
        private bool _isIntentionalLogout = false;

        // UI Panel rendering
        private RenderTarget2D _characterPanelSurface;
        private bool _panelNeedsRedraw = true;
        private Rectangle _characterPanelRect;
        private Rectangle _buttonSectionRect;
        private Rectangle _characterListRect;
        private List<Rectangle> _characterCardRects = new List<Rectangle>();
        private int _hoveredCardIndex = -1;
        private bool _previousMousePressed = false;

        // Constructors
        public SelectCharacterScene(List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters, NetworkManager networkManager)
        {
            _characters = characters ?? new List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)>();
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _logger = MuGame.AppLoggerFactory.CreateLogger<SelectCharacterScene>();

            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Characters..." };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront();

            InitializeModernUI();

            try
            {
                _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[SelectCharacterScene] Background load failed: {ex.Message}");
            }

            _progressBar = new ProgressBarControl();
            Controls.Add(_progressBar);

            SubscribeToNetworkEvents();
        }

        private void DisableDayNightCycleForScene()
        {
            if (_dayNightPatched) return;

            _dayNightPatched = true;
            _previousDayNightEnabled = Constants.ENABLE_DAY_NIGHT_CYCLE;
            _previousSunDirection = Constants.SUN_DIRECTION;
            Constants.ENABLE_DAY_NIGHT_CYCLE = false;
            SunCycleManager.ResetToDefault();
        }

        private void RestoreDayNightCycle()
        {
            if (!_dayNightPatched) return;

            Constants.ENABLE_DAY_NIGHT_CYCLE = _previousDayNightEnabled;
            Constants.SUN_DIRECTION = _previousSunDirection;
            _dayNightPatched = false;
        }

        private void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_loadingScreen != null && _loadingScreen.Visible)
                {
                    _loadingScreen.Message = message;
                    _loadingScreen.Progress = progress;
                }
            });
        }

        private void InitializeModernUI()
        {
            // Previous/Next character arrows (disabled)
            _previousCharacterButton = CreateModernNavigationButton("<");
            _previousCharacterButton.Click += (s, e) => MoveSelection(-1);
            _previousCharacterButton.Enabled = false;
            _previousCharacterButton.Visible = false;
            Controls.Add(_previousCharacterButton);

            _nextCharacterButton = CreateModernNavigationButton(">");
            _nextCharacterButton.Click += (s, e) => MoveSelection(1);
            _nextCharacterButton.Enabled = false;
            _nextCharacterButton.Visible = false;
            Controls.Add(_nextCharacterButton);

            // Action buttons
            _enterGameButton = CreateModernButton("ENTER GAME", Theme.Success);
            _enterGameButton.Click += OnEnterGameButtonClick;
            Controls.Add(_enterGameButton);

            _createCharacterButton = CreateModernButton("CREATE CHARACTER", Theme.Secondary);
            _createCharacterButton.Click += OnCreateCharacterButtonClick;
            Controls.Add(_createCharacterButton);

            _deleteCharacterButton = CreateModernButton("DELETE CHARACTER", Theme.Danger);
            _deleteCharacterButton.Click += OnDeleteCharacterButtonClick;
            Controls.Add(_deleteCharacterButton);

            _exitButton = CreateModernButton("EXIT", Theme.BgLight);
            _exitButton.Click += OnExitButtonClick;
            Controls.Add(_exitButton);

            CalculatePanelLayout();
        }

        private ButtonControl CreateModernNavigationButton(string arrow)
        {
            return new ButtonControl
            {
                Text = arrow,
                FontSize = 48f,
                AutoViewSize = false,
                ViewSize = new Point(70, 70),
                BackgroundColor = Theme.BgMid,
                HoverBackgroundColor = Theme.BgLight,
                PressedBackgroundColor = Theme.BgDark,
                TextColor = Theme.Accent,
                HoverTextColor = Theme.AccentBright,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                Visible = false,
                Enabled = false,
                BorderThickness = 2,
                BorderColor = Theme.BorderInner
            };
        }

        private ButtonControl CreateModernButton(string text, Color baseColor)
        {
            return new ButtonControl
            {
                Text = text,
                FontSize = 13f,
                AutoViewSize = false,
                ViewSize = new Point(PANEL_WIDTH - INNER_PADDING * 2, BUTTON_HEIGHT),
                BackgroundColor = baseColor,
                HoverBackgroundColor = Color.Lerp(baseColor, Color.White, 0.2f),
                PressedBackgroundColor = Color.Lerp(baseColor, Color.Black, 0.2f),
                TextColor = Theme.TextWhite,
                HoverTextColor = Theme.TextWhite,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                Visible = false,
                Enabled = false,
                BorderThickness = 1,
                BorderColor = Theme.BorderInner
            };
        }

        private void CalculatePanelLayout()
        {
            int screenWidth = ViewSize.X;
            int screenHeight = ViewSize.Y;

            // Calculate panel height based on content
            int buttonSectionHeight = (BUTTON_HEIGHT + BUTTON_SPACING) * 4 + INNER_PADDING * 2; // Buttons only, no header
            int maxCharCards = Math.Min(_characters.Count, 5);
            int characterListHeight = maxCharCards * (CHAR_CARD_HEIGHT + CHAR_CARD_SPACING) + INNER_PADDING * 2;
            int totalPanelHeight = HEADER_HEIGHT + characterListHeight + buttonSectionHeight;

            // Character panel (right side)
            int panelX = screenWidth - PANEL_WIDTH - PANEL_MARGIN;
            int panelY = (screenHeight - totalPanelHeight) / 2;
            _characterPanelRect = new Rectangle(panelX, panelY, PANEL_WIDTH, totalPanelHeight);

            // Character list section (top, below header)
            int listY = panelY + HEADER_HEIGHT;
            _characterListRect = new Rectangle(panelX, listY, PANEL_WIDTH, characterListHeight);

            // Button section (bottom of panel, below character list)
            int buttonY = listY + characterListHeight;
            _buttonSectionRect = new Rectangle(panelX, buttonY, PANEL_WIDTH, buttonSectionHeight);

            // Calculate character card rectangles
            _characterCardRects.Clear();
            int cardY = listY + INNER_PADDING;
            for (int i = 0; i < _characters.Count && i < 5; i++)
            {
                _characterCardRects.Add(new Rectangle(
                    panelX + INNER_PADDING,
                    cardY,
                    PANEL_WIDTH - INNER_PADDING * 2,
                    CHAR_CARD_HEIGHT
                ));
                cardY += CHAR_CARD_HEIGHT + CHAR_CARD_SPACING;
            }
        }

        private void PositionNavigationButtons()
        {
            // Early exit if buttons not created yet (called during construction)
            if (_previousCharacterButton == null && _nextCharacterButton == null && 
                _enterGameButton == null && _createCharacterButton == null && 
                _deleteCharacterButton == null && _exitButton == null)
            {
                return;
            }

            CalculatePanelLayout();
            
            bool ready = _initialLoadComplete && (_loadingScreen == null || !_loadingScreen.Visible) && !_isSelectionInProgress;
            bool hasCharacters = _characters.Count > 0;
            bool hasSelection = !string.IsNullOrEmpty(_currentlySelectedCharacterName);
            bool canCreate = _characters.Count < 5;

            // Position navigation arrows
            if (_previousCharacterButton != null)
            {
                _previousCharacterButton.X = (ViewSize.X / 2) - 250;
                _previousCharacterButton.Y = (ViewSize.Y - _previousCharacterButton.ViewSize.Y) / 2;
            }

            if (_nextCharacterButton != null)
            {
                _nextCharacterButton.X = (ViewSize.X / 2) + 180;
                _nextCharacterButton.Y = (ViewSize.Y - _nextCharacterButton.ViewSize.Y) / 2;
            }

            // Position action buttons in button section (bottom of panel)
            int panelX = _characterPanelRect.X;
            int buttonY = _buttonSectionRect.Y + INNER_PADDING;

            // ENTER GAME button (top of button section)
            if (_enterGameButton != null)
            {
                _enterGameButton.X = panelX + INNER_PADDING;
                _enterGameButton.Y = buttonY;
                _enterGameButton.Enabled = ready && hasCharacters && hasSelection;
                _enterGameButton.Visible = ready && hasCharacters && hasSelection;
            }

            buttonY += (BUTTON_HEIGHT + BUTTON_SPACING);

            // DELETE CHARACTER button (shows when character selected)
            if (_deleteCharacterButton != null)
            {
                _deleteCharacterButton.X = panelX + INNER_PADDING;
                _deleteCharacterButton.Y = buttonY;
                _deleteCharacterButton.Enabled = ready && hasSelection;
                _deleteCharacterButton.Visible = ready && hasSelection;
                
                _logger?.LogDebug("Delete button - Ready: {Ready}, HasSelection: {HasSel}, CharName: '{Name}', Visible: {Vis}", 
                    ready, hasSelection, _currentlySelectedCharacterName, _deleteCharacterButton.Visible);
            }

            buttonY += (BUTTON_HEIGHT + BUTTON_SPACING);

            // CREATE CHARACTER button
            if (_createCharacterButton != null)
            {
                _createCharacterButton.X = panelX + INNER_PADDING;
                _createCharacterButton.Y = buttonY;
                _createCharacterButton.Enabled = ready && canCreate;
                _createCharacterButton.Visible = ready;
            }

            buttonY += (BUTTON_HEIGHT + BUTTON_SPACING);

            // EXIT button (very bottom)
            if (_exitButton != null)
            {
                _exitButton.X = panelX + INNER_PADDING;
                _exitButton.Y = buttonY;
                _exitButton.Enabled = ready && !_isSelectionInProgress;
                _exitButton.Visible = ready;
            }

            _panelNeedsRedraw = true;
        }

        private void UpdateNavigationButtonState()
        {
            // Navigation buttons are permanently disabled
            if (_previousCharacterButton != null)
            {
                _previousCharacterButton.Enabled = false;
                _previousCharacterButton.Visible = false;
            }

            if (_nextCharacterButton != null)
            {
                _nextCharacterButton.Enabled = false;
                _nextCharacterButton.Visible = false;
            }
        }

        private void MoveSelection(int direction)
        {
            if (_characters.Count == 0 || _selectWorld == null)
            {
                return;
            }

            if (!_initialLoadComplete || (_loadingScreen != null && _loadingScreen.Visible) || _isSelectionInProgress)
            {
                return;
            }

            int currentIndex = _currentCharacterIndex;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (_characters.Count == 1)
            {
                return;
            }

            int nextIndex = (currentIndex + direction) % _characters.Count;
            if (nextIndex < 0)
            {
                nextIndex += _characters.Count;
            }

            if (nextIndex == _currentCharacterIndex)
            {
                return;
            }

            _currentCharacterIndex = nextIndex;
            _selectWorld.SetActiveCharacter(_currentCharacterIndex);
            
            // Select the character when navigating with arrows
            if (_currentCharacterIndex >= 0 && _currentCharacterIndex < _characters.Count)
            {
                var selectedCharacter = _characters[_currentCharacterIndex];
                OnCharacterClickedForSelection(this, selectedCharacter.Name);
            }
            else
            {
                // Clear selection if no valid character
                _currentlySelectedCharacterName = null;
                UpdateNavigationButtonState();
            }

            _panelNeedsRedraw = true;
        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            DisableDayNightCycleForScene();
            UpdateLoadProgress("Initializing Character Selection...", 0.0f);
            _logger.LogInformation(">>> SelectCharacterScene LoadSceneContentWithProgress starting...");

            try
            {
                UpdateLoadProgress("Creating Select World...", 0.05f);
                _selectWorld = new SelectWorld();
                _selectWorld.CharacterClicked += OnCharacterClickedForSelection;
                Controls.Add(_selectWorld);

                UpdateLoadProgress("Initializing Select World (Graphics)...", 0.1f);
                await _selectWorld.Initialize();
                World = _selectWorld;
                UpdateLoadProgress("Select World Initialized.", 0.35f); // Zwiększony postęp po inicjalizacji świata
                _logger.LogInformation("--- SelectCharacterScene: SelectWorld initialized and set.");

                if (_selectWorld.Terrain != null)
                {
                    _selectWorld.Terrain.AmbientLight = 0.6f;
                }

                if (_selectWorld != null && _characters.Any())
                {
                    UpdateLoadProgress("Preparing Character Data...", 0.40f);
                    await _selectWorld.CreateCharacterObjects(_characters);

                    if (_characters.Count > 0)
                    {
                        _currentCharacterIndex = 0;
                        _selectWorld.SetActiveCharacter(_currentCharacterIndex);
                        _currentlySelectedCharacterName = _characters[0].Name;
                    }
                    else
                    {
                        _currentCharacterIndex = -1;
                    }

                    PositionNavigationButtons();
                    UpdateNavigationButtonState();

                    float characterCreationStartProgress = 0.45f;
                    float characterCreationEndProgress = 0.85f;
                    float totalCharacterProgressSpan = characterCreationEndProgress - characterCreationStartProgress;

                    if (_characters.Count > 0)
                    {
                        float progressPerCharacter = totalCharacterProgressSpan / _characters.Count;
                        for (int i = 0; i < _characters.Count; i++)
                        {
                            UpdateLoadProgress($"Configuring character {i + 1}/{_characters.Count}...", characterCreationStartProgress + (i + 1) * progressPerCharacter);
                        }
                    }
                    else
                    {
                        UpdateLoadProgress("No characters to configure.", characterCreationEndProgress);
                    }

                    UpdateLoadProgress("Character Objects Ready.", 0.90f);
                    _logger.LogInformation("--- SelectCharacterScene: CreateCharacterObjects finished.");
                }
                else
                {
                    _currentCharacterIndex = -1;
                    string message = _characters.Any()
                        ? "Error creating character objects."
                        : "No characters found on this account.";
                    _logger.LogWarning("--- SelectCharacterScene: {Message}", message);
                    UpdateLoadProgress(message, 0.90f);
                    UpdateNavigationButtonState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! SelectCharacterScene: Error during world initialization or character creation.");
                UpdateLoadProgress("Error loading character selection.", 1.0f);
                UpdateNavigationButtonState();
            }
            finally
            {
                _initialLoadComplete = true;
                UpdateNavigationButtonState();
                UpdateLoadProgress("Character Selection Ready.", 1.0f);
                _logger.LogInformation("<<< SelectCharacterScene LoadSceneContentWithProgress finished.");
            }
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _logger.LogInformation("SelectCharacterScene.AfterLoad() called.");
            if (_loadingScreen != null)
            {
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (_loadingScreen != null)
                    {
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                        if (_progressBar != null)
                        {
                            _progressBar.Visible = false;
                        }
                        PositionNavigationButtons();
                        UpdateNavigationButtonState();
                        _previousCharacterButton?.BringToFront();
                        _nextCharacterButton?.BringToFront();
                        _deleteCharacterButton?.BringToFront();
                        _createCharacterButton?.BringToFront();
                        _enterGameButton?.BringToFront();
                        _exitButton?.BringToFront();
                        Cursor?.BringToFront();
                        DebugPanel?.BringToFront();
                    }
                });
            }
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            PositionNavigationButtons();
        }

        public override async Task Load()
        {
            if (Status == GameControlStatus.Initializing)
            {
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
            else
            {
                _logger.LogDebug("SelectCharacterScene.Load() called outside of InitializeWithProgressReporting flow. Re-routing to progressive load.");
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
        }


        public void CharacterSelected(string characterName)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _logger.LogInformation("Character selection attempted while loading screen is visible. Ignoring.");
                return;
            }

            int matchedIndex = -1;
            for (int i = 0; i < _characters.Count; i++)
            {
                if (string.Equals(_characters[i].Name, characterName, StringComparison.Ordinal))
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex < 0)
            {
                _logger.LogError("Character '{CharacterName}' selected, but not found in the character list.", characterName);
                MessageWindow.Show($"Error selecting character '{characterName}'.");
                return;
            }

            _selectedCharacterInfo = _characters[matchedIndex];
            _currentCharacterIndex = matchedIndex;
            _selectWorld?.SetActiveCharacter(_currentCharacterIndex);

            ClientConnectionState currentState = _networkManager.CurrentState;
            bool canSelect = currentState == ClientConnectionState.ConnectedToGameServer ||
                             currentState == ClientConnectionState.SelectingCharacter;

            if (!canSelect)
            {
                _logger.LogWarning("Character selection attempted but NetworkManager state is not ConnectedToGameServer or SelectingCharacter. State: {State}", currentState);
                MessageWindow.Show($"Cannot select character. Invalid network state: {currentState}");
                _selectedCharacterInfo = null;
                return;
            }

            _logger.LogInformation("Character '{CharacterName}' (Class: {Class}) selected in scene. Sending request...",
                                   _selectedCharacterInfo.Value.Name, _selectedCharacterInfo.Value.Class);

            DisableInteractionDuringSelection(characterName);
            _ = _networkManager.SendSelectCharacterRequestAsync(characterName);
        }

        public override void Dispose()
        {
            _logger.LogDebug("Disposing SelectCharacterScene.");
            UnsubscribeFromNetworkEvents();
            if (_selectWorld != null)
            {
                _selectWorld.CharacterClicked -= OnCharacterClickedForSelection;
            }
            CloseCharacterCreationDialog();
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            RestoreDayNightCycle();
            base.Dispose();
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame += HandleEnteredGame;
                _networkManager.ErrorOccurred += HandleNetworkError;
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _networkManager.CharacterListReceived += HandleCharacterListReceived;
                _networkManager.LogoutResponseReceived += HandleLogoutResponseReceived;
                _logger.LogDebug("SelectCharacterScene subscribed to NetworkManager events (including LogoutResponseReceived).");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame -= HandleEnteredGame;
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _networkManager.CharacterListReceived -= HandleCharacterListReceived;
                _networkManager.LogoutResponseReceived -= HandleLogoutResponseReceived;
                _logger.LogDebug("SelectCharacterScene unsubscribed from NetworkManager events.");
            }
        }

        private void HandleLogoutResponseReceived(object sender, LogOutType logoutType)
        {
            _logger.LogInformation("SelectCharacterScene.HandleLogoutResponseReceived: Type={Type}", logoutType);
            // Intentional logout handling is now done in HandleConnectionStateChange
            // which reacts to the Disconnected state after logout
        }

        private void HandleCharacterListReceived(object sender,
            List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters)
        {
            _logger.LogInformation("SelectCharacterScene.HandleCharacterListReceived: Received {Count} characters", characters?.Count ?? 0);
            
            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene != this)
                {
                    _logger.LogWarning("Scene changed, aborting character list refresh.");
                    return;
                }

                if (characters == null || characters.Count == 0)
                {
                    _logger.LogError("Received null or empty character list.");
                    return;
                }

                try
                {
                    _logger.LogInformation("Reloading SelectCharacterScene with updated list...");
                    var newScene = new SelectCharacterScene(characters, _networkManager);
                    MuGame.Instance.ChangeScene(newScene);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading SelectCharacterScene.");
                }
            });
        }

        private void HandleEnteredGame(object sender, EventArgs e)
        {
            _logger.LogInformation(">>> SelectCharacterScene.HandleEnteredGame: Event received.");

            if (!_selectedCharacterInfo.HasValue)
            {
                _logger.LogError("!!! SelectCharacterScene.HandleEnteredGame: EnteredGame event received, but _selectedCharacterInfo is null. Cannot change to GameScene.");
                if (_loadingScreen != null)
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                        EnableInteractionAfterSelection();
                    });
                }
                return;
            }

            var characterInfo = _selectedCharacterInfo.Value;
            _logger.LogInformation("--- SelectCharacterScene.HandleEnteredGame: Scheduling scene change to GameScene for character: {Name} ({Class})",
                characterInfo.Name, characterInfo.Class);

            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("--- SelectCharacterScene.HandleEnteredGame (UI Thread): Executing scheduled scene change...");
                if (MuGame.Instance.ActiveScene == this)
                {
                    try
                    {
                        MuGame.Instance.ChangeScene(new GameScene(characterInfo, _networkManager));
                        _logger.LogInformation("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): ChangeScene to GameScene call completed.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "!!! SelectCharacterScene.HandleEnteredGame (UI Thread): Exception during ChangeScene to GameScene.");
                        EnableInteractionAfterSelection();
                    }
                }
                else
                {
                    _logger.LogWarning("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): Scene changed before execution. Aborting change to GameScene.");
                }
            });
        }

        private void HandleNetworkError(object sender, string errorMessage)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogError("SelectCharacterScene received NetworkError: {Error}", errorMessage);
                MessageWindow.Show($"Network Error: {errorMessage}");
                EnableInteractionAfterSelection();
                if (MuGame.Instance.ActiveScene == this)
                {
                    MuGame.Instance.ChangeScene<LoginScene>();
                }
            });
        }

        private void HandleConnectionStateChange(object sender, ClientConnectionState newState)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogDebug("SelectCharacterScene received ConnectionStateChanged: {NewState}", newState);
                if (newState == ClientConnectionState.Disconnected)
                {
                    if (_isIntentionalLogout)
                    {
                        _logger.LogInformation("Intentional logout - returning to LoginScene.");
                    }
                    else
                    {
                        _logger.LogWarning("Disconnected while in character selection. Returning to LoginScene.");
                        MessageWindow.Show("Connection lost.");
                    }

                    if (MuGame.Instance.ActiveScene == this)
                    {
                        MuGame.Instance.ChangeScene<LoginScene>();
                    }
                }
            });
        }

        private void DisableInteractionDuringSelection(string characterName)
        {
            _isSelectionInProgress = true;
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
                var players = _selectWorld.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    var charObj = players[i];
                    if (charObj == null) continue;
                    charObj.Interactive = false;
                }
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = false;
                }
            }
            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl { Visible = true };
                Controls.Add(_loadingScreen);
            }
            _loadingScreen.Message = $"Entering game as {characterName}...";
            _loadingScreen.Progress = 0f;
            _loadingScreen.Visible = true;
            _loadingScreen.BringToFront();
            Cursor?.BringToFront();
            UpdateNavigationButtonState();
        }

        private void EnableInteractionAfterSelection()
        {
            _isSelectionInProgress = false;
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = true;
                var players = _selectWorld.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    var charObj = players[i];
                    if (charObj == null) continue;
                    charObj.Interactive = true;
                }
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = true;
                }
            }
            _selectedCharacterInfo = null;

            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }

            UpdateNavigationButtonState();
        }

        private void OnCreateCharacterButtonClick(object sender, EventArgs e)
        {
            if (_characterCreationDialog != null)
            {
                // Dialog already open
                return;
            }

            _logger.LogInformation("Opening character creation dialog...");

            // Create and show dialog
            _characterCreationDialog = new CharacterCreationDialog();
            _characterCreationDialog.CharacterCreateRequested += OnCharacterCreateRequested;
            _characterCreationDialog.CancelRequested += OnCharacterCreationCancelled;
            
            Controls.Add(_characterCreationDialog);
            _characterCreationDialog.BringToFront();
            Cursor?.BringToFront();

            // Disable interactions with world
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
            }
            if (_createCharacterButton != null)
            {
                _createCharacterButton.Enabled = false;
            }
        }

        private void OnCharacterCreateRequested(object sender, (string Name, CharacterClassNumber Class) data)
        {
            _logger.LogInformation("Character creation requested: Name={Name}, Class={Class}", data.Name, data.Class);

            // Close dialog
            CloseCharacterCreationDialog();

            // Send create character request
            var characterService = _networkManager?.GetCharacterService();
            if (characterService != null)
            {
                _ = characterService.SendCreateCharacterRequestAsync(data.Name, data.Class);
                MessageWindow.Show($"Creating character '{data.Name}'...\nPlease wait for server response.");
                
                // Request updated character list after a short delay
                _ = RefreshCharacterListAfterDelay();
            }
            else
            {
                _logger.LogError("CharacterService not available - cannot create character.");
                MessageWindow.Show("Error: Cannot create character at this time.");
            }
        }

        private async Task RefreshCharacterListAfterDelay()
        {
            // Wait for server to process creation
            await Task.Delay(2000);
            
            _logger.LogInformation("Requesting updated character list after creation...");
            var characterService = _networkManager?.GetCharacterService();
            if (characterService != null)
            {
                await characterService.RequestCharacterListAsync();
                // Note: The character list handler will update the scene
            }
        }

        private void OnCharacterCreationCancelled(object sender, EventArgs e)
        {
            _logger.LogInformation("Character creation cancelled.");
            CloseCharacterCreationDialog();
        }
        
        private void OnCharacterClickedForSelection(object sender, string characterName)
        {
            _logger.LogInformation("Character '{Name}' selected.", characterName);
            _currentlySelectedCharacterName = characterName;
            
            // Update button states
            PositionNavigationButtons();
            UpdateNavigationButtonState();
            
            // Redraw panel
            _panelNeedsRedraw = true;
            
            _logger.LogInformation("Character selected - Buttons updated. Delete: {Visible}, Enter: {Enabled}", 
                _deleteCharacterButton?.Visible, _deleteCharacterButton?.Enabled);
        }
        
        private void OnDeleteCharacterButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentlySelectedCharacterName))
            {
                _logger.LogWarning("Delete button clicked but no character selected.");
                return;
            }
            
            string characterToDelete = _currentlySelectedCharacterName;
            _logger.LogInformation("Delete button clicked for character '{Name}'.", characterToDelete);
            
            // Create security code input dialog
            var securityCodeDialog = new CharacterDeletionDialog(characterToDelete);
            securityCodeDialog.DeleteConfirmed += (s, securityCode) =>
            {
                _logger.LogInformation("User confirmed deletion of '{Name}' with security code.", characterToDelete);
                var characterService = _networkManager?.GetCharacterService();
                if (characterService != null)
                {
                    _ = characterService.SendDeleteCharacterRequestAsync(characterToDelete, securityCode);
                    MessageWindow.Show($"Deleting character '{characterToDelete}'...\nPlease wait for server response.");
                    
                    // Clear selection
                    _currentlySelectedCharacterName = null;
                    UpdateNavigationButtonState();
                    _panelNeedsRedraw = true;
                }
                else
                {
                    _logger.LogError("CharacterService not available - cannot delete character.");
                    MessageWindow.Show("Error: Cannot delete character at this time.");
                }
                
                // Clean up dialog
                Controls.Remove(securityCodeDialog);
                securityCodeDialog.Dispose();
                
                // Re-enable world interaction
                if (_selectWorld != null)
                {
                    _selectWorld.Interactive = true;
                }
            };
            
            securityCodeDialog.CancelRequested += (s, args) =>
            {
                _logger.LogInformation("User cancelled deletion of '{Name}'.", characterToDelete);
                
                // Clean up dialog
                Controls.Remove(securityCodeDialog);
                securityCodeDialog.Dispose();
                
                // Re-enable world interaction
                if (_selectWorld != null)
                {
                    _selectWorld.Interactive = true;
                }
            };
            
            // Show dialog
            Controls.Add(securityCodeDialog);
            securityCodeDialog.BringToFront();
            Cursor?.BringToFront();
            
            // Disable world interaction while dialog is open
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
            }
        }

        private void CloseCharacterCreationDialog()
        {
            if (_characterCreationDialog != null)
            {
                _characterCreationDialog.CharacterCreateRequested -= OnCharacterCreateRequested;
                _characterCreationDialog.CancelRequested -= OnCharacterCreationCancelled;
                Controls.Remove(_characterCreationDialog);
                _characterCreationDialog.Dispose();
                _characterCreationDialog = null;
            }

            // Re-enable interactions
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = true;
            }
            UpdateNavigationButtonState();
        }

        public override void Update(GameTime gameTime)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _loadingScreen.Update(gameTime);
                Cursor?.Update(gameTime);
                DebugPanel?.Update(gameTime);
                return;
            }
            if (!_initialLoadComplete && Status == GameControlStatus.Initializing)
            {
                Cursor?.Update(gameTime);
                DebugPanel?.Update(gameTime);
                return;
            }

            // Handle character card mouse interaction
            UpdateCharacterCardInteraction();

            base.Update(gameTime);
        }

        private void UpdateCharacterCardInteraction()
        {
            if (_characterCardRects.Count == 0 || !_initialLoadComplete || Cursor == null)
                return;

            Point mousePos = new Point((int)Cursor.X, (int)Cursor.Y);
            int previousHovered = _hoveredCardIndex;
            _hoveredCardIndex = -1;

            var mouseState = MuGame.Instance.UiMouseState;
            bool mousePressed = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            bool mouseClicked = mousePressed && !_previousMousePressed;
            _previousMousePressed = mousePressed;

            // Only check cards if mouse is in the character list area
            if (!_characterListRect.Contains(mousePos))
                return;

            // Check if mouse is over any character card
            for (int i = 0; i < _characterCardRects.Count; i++)
            {
                if (_characterCardRects[i].Contains(mousePos))
                {
                    _hoveredCardIndex = i;
                    
                    if (previousHovered != _hoveredCardIndex)
                    {
                        _panelNeedsRedraw = true;
                    }

                    // Handle click (on mouse release)
                    if (mouseClicked)
                    {
                        SelectCharacterByIndex(i);
                        _logger.LogInformation("Character card {Index} clicked: {Name}", i, _characters[i].Name);
                    }
                    break;
                }
            }

            if (previousHovered != _hoveredCardIndex && previousHovered != -1)
            {
                _panelNeedsRedraw = true;
            }
        }

        private void SelectCharacterByIndex(int index)
        {
            if (index < 0 || index >= _characters.Count)
                return;

            _currentCharacterIndex = index;
            var character = _characters[index];
            _currentlySelectedCharacterName = character.Name;
            _selectWorld?.SetActiveCharacter(_currentCharacterIndex);
            PositionNavigationButtons();
            UpdateNavigationButtonState();
            _panelNeedsRedraw = true;

            _logger.LogInformation("Character '{Name}' selected via card click.", character.Name);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                GraphicsDevice.Clear(new Color(12, 12, 20));
                DrawBackground();
                _progressBar.Progress = _loadingScreen.Progress;
                _progressBar.StatusText = _loadingScreen.Message;
                _progressBar.Visible = true;
                _progressBar.Draw(gameTime);
                return;
            }

            // Draw 3D world first
            base.Draw(gameTime);

            // Draw modern UI overlay
            DrawModernUI(gameTime);
        }

        private void DrawModernUI(GameTime gameTime)
        {
            // Draw character info panel
            using (var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform))
            {
                var sb = GraphicsManager.Instance.Sprite;
                DrawCharacterPanel(sb);
            }

            // Draw cursor and debug panel on top of everything
            using (var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform))
            {
                Cursor?.Draw(gameTime);
                DebugPanel?.Draw(gameTime);
            }
        }

        private void DrawCharacterPanel(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            var font = GraphicsManager.Instance.Font;
            if (pixel == null || font == null) return;

            // Panel background excluding button section (so buttons are visible on top)
            var panelWithoutButtons = new Rectangle(
                _characterPanelRect.X,
                _characterPanelRect.Y,
                _characterPanelRect.Width,
                _characterPanelRect.Height - _buttonSectionRect.Height
            );
            UiDrawHelper.DrawVerticalGradient(sb, panelWithoutButtons, Theme.BgMid, Theme.BgDark);
            
            // Outer border (excluding button section - no bottom border, side borders stop at character list)
            int borderEndY = _characterListRect.Bottom;
            sb.Draw(pixel, new Rectangle(_characterPanelRect.X - 1, _characterPanelRect.Y - 1, _characterPanelRect.Width + 2, 1), Theme.BorderOuter); // Top border
            sb.Draw(pixel, new Rectangle(_characterPanelRect.X - 1, _characterPanelRect.Y, 1, borderEndY - _characterPanelRect.Y), Theme.BorderOuter); // Left border (stops at character list)
            sb.Draw(pixel, new Rectangle(_characterPanelRect.Right, _characterPanelRect.Y, 1, borderEndY - _characterPanelRect.Y), Theme.BorderOuter); // Right border (stops at character list)

            // Header section
            var headerRect = new Rectangle(_characterPanelRect.X, _characterPanelRect.Y, _characterPanelRect.Width, HEADER_HEIGHT);
            UiDrawHelper.DrawHorizontalGradient(sb, headerRect, Theme.BgLighter, Theme.BgMid);
            UiDrawHelper.DrawCornerAccents(sb, headerRect, Theme.Accent, 12, 2);
            
            // Header separator
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom - 1, headerRect.Width, 1), Theme.BorderInner);
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom - 2, headerRect.Width, 1), Theme.Accent * 0.3f);

            // Header text
            string headerText = "CHARACTERS";
            Vector2 headerTextSize = font.MeasureString(headerText) * 0.75f;
            Vector2 headerTextPos = new Vector2(
                headerRect.X + (headerRect.Width - headerTextSize.X) / 2,
                headerRect.Y + (headerRect.Height - headerTextSize.Y) / 2
            );
            sb.DrawString(font, headerText, headerTextPos + new Vector2(1, 1), Color.Black * 0.7f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
            sb.DrawString(font, headerText, headerTextPos, Theme.TextGold, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

            // Draw character list separator (top)
            sb.Draw(pixel, new Rectangle(_characterListRect.X, _characterListRect.Y, _characterListRect.Width, 1), Theme.BorderInner);
            
            // Draw separator between character list and buttons (bottom)
            sb.Draw(pixel, new Rectangle(_characterListRect.X, _characterListRect.Bottom, _characterListRect.Width, 1), Theme.BorderInner);

            // Draw character cards
            for (int i = 0; i < _characters.Count && i < _characterCardRects.Count; i++)
            {
                DrawCharacterCard(sb, pixel, font, i, _characterCardRects[i], _characters[i]);
            }
        }

        private void DrawCharacterCard(SpriteBatch sb, Texture2D pixel, SpriteFont font, int index, Rectangle cardRect, (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) character)
        {
            bool isSelected = _currentCharacterIndex == index;
            bool isHovered = _hoveredCardIndex == index;

            // Card background
            Color bgColor = isSelected ? Theme.BgLighter : (isHovered ? Theme.BgMid : Theme.BgDark);
            sb.Draw(pixel, cardRect, bgColor);

            // Card border
            Color borderColor = isSelected ? Theme.Accent : Theme.BorderInner;
            int borderWidth = isSelected ? 2 : 1;
            sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, cardRect.Width, borderWidth), borderColor);
            sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Bottom - borderWidth, cardRect.Width, borderWidth), borderColor);
            sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, borderWidth, cardRect.Height), borderColor);
            sb.Draw(pixel, new Rectangle(cardRect.Right - borderWidth, cardRect.Y, borderWidth, cardRect.Height), borderColor);

            // Character info
            int textX = cardRect.X + 10;
            int textY = cardRect.Y + 10;
            float nameScale = 0.7f;
            float infoScale = 0.6f;

            // Name
            Color nameColor = isSelected ? Theme.TextGold : Theme.TextWhite;
            sb.DrawString(font, character.Name, new Vector2(textX, textY) + new Vector2(1, 1), Color.Black * 0.7f, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            sb.DrawString(font, character.Name, new Vector2(textX, textY), nameColor, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            textY += 22;

            // Class and Level
            string classLevelText = $"{character.Class}  •  Lv.{character.Level}";
            Color infoColor = isSelected ? Theme.AccentBright : Theme.TextGray;
            sb.DrawString(font, classLevelText, new Vector2(textX, textY) + new Vector2(1, 1), Color.Black * 0.7f, 0f, Vector2.Zero, infoScale, SpriteEffects.None, 0f);
            sb.DrawString(font, classLevelText, new Vector2(textX, textY), infoColor, 0f, Vector2.Zero, infoScale, SpriteEffects.None, 0f);
        }

        private new void DrawBackground()
        {
            if (_backgroundTexture == null) return;

            using var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform);

            GraphicsManager.Instance.Sprite.Draw(_backgroundTexture,
                new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y), Color.White);
        }

        private void OnEnterGameButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentlySelectedCharacterName))
            {
                MessageWindow.Show("Please select a character first.");
                return;
            }

            // Enter game with selected character
            var matchedIndex = _characters.FindIndex(c => c.Name.Equals(_currentlySelectedCharacterName, StringComparison.OrdinalIgnoreCase));
            if (matchedIndex < 0)
            {
                _logger.LogWarning("Character '{Name}' not found in character list.", _currentlySelectedCharacterName);
                MessageWindow.Show($"Error: Character '{_currentlySelectedCharacterName}' not found.");
                return;
            }

            _selectedCharacterInfo = _characters[matchedIndex];
            _currentCharacterIndex = matchedIndex;
            _selectWorld?.SetActiveCharacter(_currentCharacterIndex);

            ClientConnectionState currentState = _networkManager.CurrentState;
            bool canSelect = currentState == ClientConnectionState.ConnectedToGameServer ||
                             currentState == ClientConnectionState.SelectingCharacter;

            if (!canSelect)
            {
                _logger.LogWarning("Character selection attempted but NetworkManager state is not ConnectedToGameServer or SelectingCharacter. State: {State}", currentState);
                MessageWindow.Show($"Cannot select character. Invalid network state: {currentState}");
                _selectedCharacterInfo = null;
                return;
            }

            _logger.LogInformation("Character '{CharacterName}' (Class: {Class}) selected in scene. Sending request...",
                                   _selectedCharacterInfo.Value.Name, _selectedCharacterInfo.Value.Class);

            DisableInteractionDuringSelection(_currentlySelectedCharacterName);
            _ = _networkManager.SendSelectCharacterRequestAsync(_currentlySelectedCharacterName);
        }

        private void OnExitButtonClick(object sender, EventArgs e)
        {
            _logger.LogInformation("Exit button clicked - returning to login.");
            _isIntentionalLogout = true;
            _ = _networkManager.GetCharacterService().SendLogoutRequestAsync(LogOutType.BackToServerSelection);
        }
    }
}
