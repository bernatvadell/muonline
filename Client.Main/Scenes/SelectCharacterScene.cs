using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game;
using Client.Main.Core.Client;
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class SelectCharacterScene : BaseScene
    {
        // Fields
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _characters;
        private SelectWorld _selectWorld;
        private LabelControl _infoLabel;
        private readonly NetworkManager _networkManager;
        private ILogger<SelectCharacterScene> _logger;
        private (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)? _selectedCharacterInfo = null;
        private LoadingScreenControl _loadingScreen;
        private bool _initialLoadComplete = false;
        private ButtonControl _previousCharacterButton;
        private ButtonControl _nextCharacterButton;
        private int _currentCharacterIndex = -1;
        private bool _isSelectionInProgress = false;

        private const int NavigationButtonSize = 64;
        private const int NavigationHorizontalOffset = 200;

        // Constructors
        public SelectCharacterScene(List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters, NetworkManager networkManager)
        {
            _characters = characters ?? new List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)>();
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _logger = MuGame.AppLoggerFactory.CreateLogger<SelectCharacterScene>();

            _infoLabel = new LabelControl
            {
                Text = "Preparing character selection...",
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                Margin = new Margin { Top = 20 },
                FontSize = 16,
                TextColor = Color.LightGray,
                Visible = false
            };
            Controls.Add(_infoLabel);

            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Characters..." };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront();

            InitializeNavigationControls();

            SubscribeToNetworkEvents();
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

        private void InitializeNavigationControls()
        {
            if (_previousCharacterButton != null || _nextCharacterButton != null)
            {
                PositionNavigationButtons();
                UpdateNavigationButtonState();
                return;
            }

            _previousCharacterButton = CreateNavigationButton("CharacterNavLeft", "<<");
            _previousCharacterButton.Click += (s, e) => MoveSelection(-1);
            Controls.Add(_previousCharacterButton);

            _nextCharacterButton = CreateNavigationButton("CharacterNavRight", ">>");
            _nextCharacterButton.Click += (s, e) => MoveSelection(1);
            Controls.Add(_nextCharacterButton);

            _previousCharacterButton.BringToFront();
            _nextCharacterButton.BringToFront();
            PositionNavigationButtons();
            UpdateNavigationButtonState();
        }

        private ButtonControl CreateNavigationButton(string name, string text)
        {
            return new ButtonControl
            {
                Name = name,
                Text = text,
                FontSize = 32f,
                AutoViewSize = false,
                ViewSize = new Point(NavigationButtonSize, NavigationButtonSize),
                // MuOnline-style golden/bronze gradient background
                BackgroundColor = new Color(139, 69, 19, 220),      // Bronze base
                HoverBackgroundColor = new Color(218, 165, 32, 240), // Golden rod hover
                PressedBackgroundColor = new Color(184, 134, 11, 255), // Dark golden rod pressed
                // Enhanced text styling for better visibility
                TextColor = new Color(255, 248, 220),               // Cornsilk - warm white
                HoverTextColor = new Color(255, 215, 0),            // Gold
                DisabledTextColor = new Color(105, 105, 105, 150),  // Dim gray
                Interactive = true,
                Visible = false,
                Enabled = false,
                TextPaddingX = 0,
                TextPaddingY = -2, // Slight upward adjustment for better centering
                // Enhanced border for that medieval/fantasy look
                BorderThickness = 2,
                BorderColor = new Color(255, 215, 0, 180)           // Gold border
            };
        }

        private void PositionNavigationButtons()
        {
            if (_previousCharacterButton != null)
            {
                _previousCharacterButton.X = (ViewSize.X / 2) - 200 - NavigationHorizontalOffset - _previousCharacterButton.ViewSize.X;
                _previousCharacterButton.Y = (ViewSize.Y - _previousCharacterButton.ViewSize.Y) / 2;
            }

            if (_nextCharacterButton != null)
            {
                _nextCharacterButton.X = (ViewSize.X / 2) + NavigationHorizontalOffset;
                _nextCharacterButton.Y = (ViewSize.Y - _nextCharacterButton.ViewSize.Y) / 2;
            }
        }

        private void UpdateNavigationButtonState()
        {
            bool hasCharacters = _characters.Count > 0;
            bool multipleCharacters = _characters.Count > 1;
            bool ready = _initialLoadComplete && (_loadingScreen == null || !_loadingScreen.Visible) && !_isSelectionInProgress;

            if (_previousCharacterButton != null)
            {
                _previousCharacterButton.Enabled = ready && multipleCharacters;
                _previousCharacterButton.Visible = ready && hasCharacters && multipleCharacters;
            }

            if (_nextCharacterButton != null)
            {
                _nextCharacterButton.Enabled = ready && multipleCharacters;
                _nextCharacterButton.Visible = ready && hasCharacters && multipleCharacters;
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
            UpdateSelectionLabel();
        }

        private void UpdateSelectionLabel()
        {
            if (_infoLabel == null)
            {
                return;
            }

            if (_currentCharacterIndex >= 0 && _currentCharacterIndex < _characters.Count)
            {
                var character = _characters[_currentCharacterIndex];
                _infoLabel.Text = $"Select your character: {character.Name} (Lv.{character.Level})";
            }
            else
            {
                _infoLabel.Text = _characters.Any() ? "Select your character" : "No characters found on this account.";
            }
        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            UpdateLoadProgress("Initializing Character Selection...", 0.0f);
            _logger.LogInformation(">>> SelectCharacterScene LoadSceneContentWithProgress starting...");

            try
            {
                UpdateLoadProgress("Creating Select World...", 0.05f);
                _selectWorld = new SelectWorld();
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
                        UpdateSelectionLabel();
                    }
                    else
                    {
                        _currentCharacterIndex = -1;
                        UpdateSelectionLabel();
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
                    UpdateSelectionLabel();
                    string message = _characters.Any()
                        ? "Error creating character objects."
                        : "No characters found on this account.";
                    if (_infoLabel != null) _infoLabel.Text = message;
                    _logger.LogWarning("--- SelectCharacterScene: {Message}", message);
                    UpdateLoadProgress(message, 0.90f);
                    UpdateNavigationButtonState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! SelectCharacterScene: Error during world initialization or character creation.");
                if (_infoLabel != null) _infoLabel.Text = "Error loading character selection.";
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
                        _infoLabel.Visible = true;
                        UpdateSelectionLabel();
                        PositionNavigationButtons();
                        UpdateNavigationButtonState();
                        _infoLabel?.BringToFront();
                        _previousCharacterButton?.BringToFront();
                        _nextCharacterButton?.BringToFront();
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
            UpdateSelectionLabel();

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
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            base.Dispose();
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame += HandleEnteredGame;
                _networkManager.ErrorOccurred += HandleNetworkError;
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _logger.LogDebug("SelectCharacterScene subscribed to NetworkManager events.");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame -= HandleEnteredGame;
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _logger.LogDebug("SelectCharacterScene unsubscribed from NetworkManager events.");
            }
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
                    _logger.LogWarning("Disconnected while in character selection. Returning to LoginScene.");
                    MessageWindow.Show("Connection lost.");
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
                foreach (var charObj in _selectWorld.Objects.OfType<PlayerObject>())
                {
                    charObj.Interactive = false;
                }
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = false;
                }
            }
            if (_infoLabel != null)
            {
                _infoLabel.Text = $"Selecting {characterName}...";
                _infoLabel.Visible = true;
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
                foreach (var charObj in _selectWorld.Objects.OfType<PlayerObject>())
                {
                    charObj.Interactive = true;
                }
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = true;
                }
            }
            if (_infoLabel != null)
            {
                UpdateSelectionLabel();
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
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
