using Client.Main.Controls.UI;
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
        private NetworkManager _networkManager;
        private ILogger<SelectCharacterScene> _logger;
        private (string Name, CharacterClassNumber Class, ushort Level)? _selectedCharacterInfo = null;

        // Constructors
        public SelectCharacterScene(List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
            _characters = characters ?? new List<(string Name, CharacterClassNumber Class, ushort Level)>();
            _networkManager = MuGame.Network;
            _logger = MuGame.AppLoggerFactory.CreateLogger<SelectCharacterScene>();

            _infoLabel = new LabelControl
            {
                Text = "Loading character selection...",
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                Margin = new Margin { Top = 20 },
                FontSize = 16,
                TextColor = Color.LightGray
            };
            Controls.Add(_infoLabel);

            SubscribeToNetworkEvents();
        }

        // Public Methods
        public override async Task Load()
        {
            _logger.LogInformation(">>> SelectCharacterScene Load starting...");
            await base.Load();
            try
            {
                _selectWorld = new SelectWorld();
                Controls.Add(_selectWorld);
                _logger.LogInformation("--- SelectCharacterScene Load: Calling _selectWorld.Initialize()...");
                await _selectWorld.Initialize();
                World = _selectWorld;
                _logger.LogInformation("--- SelectCharacterScene Load: SelectWorld initialized and set.");

                if (_selectWorld != null && _characters.Any())
                {
                    _logger.LogInformation("--- SelectCharacterScene Load: Calling _selectWorld.CreateCharacterObjects()...");
                    await _selectWorld.CreateCharacterObjects(_characters);
                    if (_infoLabel != null) _infoLabel.Text = "Select your character";
                    _logger.LogInformation("--- SelectCharacterScene Load: CreateCharacterObjects finished.");
                }
                else
                {
                    string message = _characters.Any()
                        ? "Error creating character objects."
                        : "No characters found on this account.";
                    if (_infoLabel != null) _infoLabel.Text = message;
                    _logger.LogWarning("--- SelectCharacterScene Load: {Message}", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! SelectCharacterScene Load: Error during world initialization or character creation.");
                if (_infoLabel != null) _infoLabel.Text = "Error loading character selection.";
            }

            Cursor?.BringToFront();
            _infoLabel?.BringToFront();
            _logger.LogInformation("<<< SelectCharacterScene Load finished.");
        }

        public void CharacterSelected(string characterName)
        {
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
                _logger.LogWarning("Character selection attempted but NetworkManager state is not ConnectedToGameServer. State: {State}", currentState);
                MessageWindow.Show($"Cannot select character. Invalid network state: {currentState}");
                _selectedCharacterInfo = null; // Clear selection on error
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
            base.Dispose();
        }

        // Private Methods
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
                        MuGame.Instance.ChangeScene(new GameScene(characterInfo));
                        _logger.LogInformation("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): ChangeScene to GameScene call completed.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "!!! SelectCharacterScene.HandleEnteredGame (UI Thread): Exception during ChangeScene to GameScene.");
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
                // Also hide/disable labels on selection
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = false;
                }
            }
            if (_infoLabel != null)
            {
                _infoLabel.Text = $"Selecting {characterName}...";
            }
        }
    }
}