using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes.SelectCharacter
{
    public class CharacterSelectionController : IDisposable
    {
        // === Private state ===
        private readonly List<PlayerObject> _characters = new();
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _characterInfos = new();
        private readonly Dictionary<PlayerObject, LabelControl> _labels = new();
        private readonly ILogger<CharacterSelectionController> _logger;
        private int _activeIndex = -1;

        // Double-click detection
        private DateTime _lastClickTime = DateTime.MinValue;
        private string _lastClickedCharacter;
        private const double DoubleClickThresholdMs = 500;

        // Random emote
        private readonly Random _random = new();

        // === Public data (read-only) ===
        public IReadOnlyList<PlayerObject> Characters => _characters;
        public IReadOnlyDictionary<PlayerObject, LabelControl> Labels => _labels;

        // === State ===
        public int ActiveIndex => _activeIndex;
        public PlayerObject ActiveCharacter =>
            _activeIndex >= 0 && _activeIndex < _characters.Count
                ? _characters[_activeIndex]
                : null;

        // === Events ===
        public event EventHandler<string> CharacterClicked;
        public event EventHandler<string> CharacterDoubleClicked;

        // === Constructor ===
        public CharacterSelectionController(ILogger<CharacterSelectionController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // === Character Creation ===
        public async Task CreateCharactersAsync(
            List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characterInfos,
            WorldControl world,
            GameControl scene,
            Vector3 displayPosition,
            Vector3 displayAngle)
        {
            _logger.LogInformation("Creating {Count} character objects...", characterInfos.Count);

            // Dispose old objects
            DisposeCharacters(world, scene);

            _characterInfos.Clear();
            _characterInfos.AddRange(characterInfos);
            _activeIndex = -1;

            if (characterInfos.Count == 0)
            {
                _logger.LogInformation("No characters provided for selection.");
                return;
            }

            var loading = new List<Task>(characterInfos.Count);

            foreach (var (name, cls, lvl, appearanceBytes) in characterInfos)
            {
                var player = new PlayerObject(new AppearanceData(appearanceBytes))
                {
                    Name = name,
                    CharacterClass = cls,
                    Position = displayPosition,
                    Angle = displayAngle,
                    Interactive = false,
                    World = world,
                    CurrentAction = PlayerAction.PlayerStopMale,
                    Hidden = true,
                };

                player.Click += OnPlayerClick;

                _characters.Add(player);
                world.Objects.Add(player);
                loading.Add(player.Load());

                var label = new LabelControl
                {
                    Text = $"Lv.{lvl}  {name}",
                    FontSize = 14,
                    TextColor = Color.White,
                    HasShadow = true,
                    ShadowColor = Color.Black * 0.8f,
                    ShadowOffset = new Vector2(1, 1),
                    UseManualPosition = true,
                    Visible = false
                };

                _labels.Add(player, label);
                scene.Controls.Add(label);
                label.BringToFront();
            }

            await Task.WhenAll(loading);

            // Note: Cursor.BringToFront() is handled by the scene after creation

            _logger.LogInformation("Finished creating and loading character objects and labels.");

            if (_characters.Count > 0)
            {
                SetActiveCharacter(0);
            }
        }

        // Overload for TestAnimationScene compatibility (uses PlayerClass and AppearanceConfig)
        public async Task CreateCharactersAsync(
            List<(string Name, PlayerClass Class, ushort Level, AppearanceConfig Appearance)> characters,
            WorldControl world,
            GameControl scene,
            Vector3 displayPosition,
            Vector3 displayAngle)
        {
            _logger.LogInformation("Creating {Count} character objects (AppearanceConfig version)...", characters.Count);

            // Dispose old objects
            DisposeCharacters(world, scene);

            _characterInfos.Clear();
            var converted = characters.Select(p => (p.Name, (CharacterClassNumber)p.Class, p.Level, Array.Empty<byte>()));
            _characterInfos.AddRange(converted);
            _activeIndex = -1;

            if (characters.Count == 0)
            {
                _logger.LogInformation("No characters provided for selection.");
                return;
            }

            foreach (var (name, cls, lvl, appearanceConfig) in characters)
            {
                var player = new PlayerObject(new AppearanceData())
                {
                    Name = name,
                    CharacterClass = CharacterClassNumber.DarkWizard,
                    Position = displayPosition,
                    Angle = displayAngle,
                    Interactive = false,
                    World = world,
                    CurrentAction = PlayerAction.PlayerStopMale,
                    Hidden = true
                };

                player.Click += OnPlayerClick;

                _characters.Add(player);
                world.Objects.Add(player);
                await player.Load(appearanceConfig.PlayerClass);
                await player.UpdateEquipmentAppearanceFromConfig(appearanceConfig);

                var label = new LabelControl
                {
                    Text = $"Lv.{lvl}  {name}",
                    FontSize = 14,
                    TextColor = Color.White,
                    HasShadow = true,
                    ShadowColor = Color.Black * 0.8f,
                    ShadowOffset = new Vector2(1, 1),
                    UseManualPosition = true,
                    Visible = false
                };

                _labels.Add(player, label);
                scene.Controls.Add(label);
                label.BringToFront();
            }

            _logger.LogInformation("Finished creating and loading character objects and labels.");

            if (_characters.Count > 0)
            {
                SetActiveCharacter(0);
            }
        }

        // === Active Character Management ===
        public void SetActiveCharacter(int index)
        {
            if (_characters.Count == 0)
            {
                _activeIndex = -1;
                return;
            }

            if (index < 0 || index >= _characters.Count)
            {
                _logger.LogWarning("Attempted to activate character at invalid index {Index}", index);
                return;
            }

            if (_activeIndex == index)
            {
                return;
            }

            for (int i = 0; i < _characters.Count; i++)
            {
                var player = _characters[i];
                bool isActive = i == index;

                player.Hidden = !isActive;
                player.Interactive = isActive;

                if (_labels.TryGetValue(player, out var label))
                {
                    label.Visible = isActive;
                }
            }

            _activeIndex = index;

            // Play a random emote animation when character is selected
            var activePlayer = _characters[index];
            if (activePlayer != null && !activePlayer.Hidden)
            {
                PlayRandomEmote(activePlayer);
            }
        }

        // === Emote Animations ===
        private void PlayRandomEmote(PlayerObject player)
        {
            if (player == null || player.Hidden)
                return;

            if (player.IsOneShotPlaying)
                return;

            bool isFemale = PlayerActionMapper.IsCharacterFemale(player.CharacterClass);
            var availableEmotes = isFemale
                ? new[] { PlayerAction.PlayerSeeFemale1, PlayerAction.PlayerWinFemale1, PlayerAction.PlayerSmileFemale1 }
                : new[] { PlayerAction.PlayerSee1, PlayerAction.PlayerWin1, PlayerAction.PlayerSmile1 };

            var randomEmote = availableEmotes[_random.Next(availableEmotes.Length)];

            _logger.LogDebug("Playing random emote {Emote} for character {CharacterName} (Female: {IsFemale})",
                randomEmote, player.Name, isFemale);

            player.PlayEmoteAnimation(randomEmote);
        }

        public void PlayEmoteAnimation(PlayerAction action)
        {
            var activePlayer = ActiveCharacter;
            if (activePlayer == null || activePlayer.Hidden || activePlayer.IsOneShotPlaying)
                return;

            activePlayer.PlayEmoteAnimation(action);
        }

        // === Click Handling ===
        private void OnPlayerClick(object sender, EventArgs e)
        {
            PlayerObject clickedPlayer = null;

            if (sender is PlayerObject player)
            {
                clickedPlayer = player;
            }
            else if (sender is ModelObject bodyPart && bodyPart.Parent is PlayerObject parentPlayer)
            {
                clickedPlayer = parentPlayer;
            }

            if (clickedPlayer == null)
                return;

            if (_activeIndex < 0 || _characters[_activeIndex] != clickedPlayer)
            {
                _logger.LogDebug("Ignoring click on inactive character '{Name}'.", clickedPlayer.Name);
                return;
            }

            // Check for double-click
            DateTime now = DateTime.UtcNow;
            double timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
            bool isDoubleClick = timeSinceLastClick < DoubleClickThresholdMs &&
                                _lastClickedCharacter == clickedPlayer.Name;

            _lastClickTime = now;
            _lastClickedCharacter = clickedPlayer.Name;

            if (isDoubleClick)
            {
                _logger.LogInformation("Character '{Name}' double-clicked - joining game.", clickedPlayer.Name);
                CharacterDoubleClicked?.Invoke(this, clickedPlayer.Name);
            }
            else
            {
                _logger.LogInformation("Character '{Name}' clicked.", clickedPlayer.Name);
                CharacterClicked?.Invoke(this, clickedPlayer.Name);
            }
        }

        // === Cleanup ===
        private void DisposeCharacters(WorldControl world, GameControl scene)
        {
            foreach (var player in _characters)
            {
                player.Click -= OnPlayerClick;
                world?.Objects.Remove(player);
                player.Dispose();
            }
            _characters.Clear();

            foreach (var label in _labels.Values)
            {
                scene?.Controls.Remove(label);
                label.Dispose();
            }
            _labels.Clear();
        }

        public void Dispose()
        {
            foreach (var player in _characters)
            {
                player.Click -= OnPlayerClick;
                player.Dispose();
            }
            _characters.Clear();

            foreach (var label in _labels.Values)
            {
                label.Dispose();
            }
            _labels.Clear();

            _characterInfos.Clear();
            _activeIndex = -1;
        }
    }
}
