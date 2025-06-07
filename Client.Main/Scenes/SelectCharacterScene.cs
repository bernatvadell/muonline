using Client.Main.Controls.UI;
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
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level)> _characters;
        private SelectWorld _selectWorld;
        private LabelControl _infoLabel;
        private readonly NetworkManager _networkManager;
        private ILogger<SelectCharacterScene> _logger;
        private (string Name, CharacterClassNumber Class, ushort Level)? _selectedCharacterInfo = null;
        private LoadingScreenControl _loadingScreen;
        private bool _initialLoadComplete = false;

        // Constructors
        public SelectCharacterScene(List<(string Name, CharacterClassNumber Class, ushort Level)> characters, NetworkManager networkManager)
        {
            _characters = characters ?? new List<(string Name, CharacterClassNumber Class, ushort Level)>();
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


                    if (_infoLabel != null) _infoLabel.Text = "Select your character";
                    UpdateLoadProgress("Character Objects Ready.", 0.90f);
                    _logger.LogInformation("--- SelectCharacterScene: CreateCharacterObjects finished.");
                }
                else
                {
                    string message = _characters.Any()
                        ? "Error creating character objects."
                        : "No characters found on this account.";
                    if (_infoLabel != null) _infoLabel.Text = message;
                    _logger.LogWarning("--- SelectCharacterScene: {Message}", message);
                    UpdateLoadProgress(message, 0.90f);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! SelectCharacterScene: Error during world initialization or character creation.");
                if (_infoLabel != null) _infoLabel.Text = "Error loading character selection.";
                UpdateLoadProgress("Error loading character selection.", 1.0f);
            }
            finally
            {
                _initialLoadComplete = true;
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
                        Cursor?.BringToFront();
                        _infoLabel?.BringToFront();
                        DebugPanel?.BringToFront();
                    }
                });
            }
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

            _selectedCharacterInfo = _characters.FirstOrDefault(c => c.Name == characterName);

            if (!_selectedCharacterInfo.HasValue)
            {
                _logger.LogError("Character '{CharacterName}' selected, but not found in the character list.", characterName);
                MessageWindow.Show($"Error selecting character '{characterName}'.");
                return;
            }

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
        }

        private void EnableInteractionAfterSelection()
        {
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
                _infoLabel.Text = "Select your character";
            }
            _selectedCharacterInfo = null;

            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
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