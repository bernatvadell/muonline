using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI; // Added for LabelControl
using Client.Main.Models; // Added for LabelControl
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Client.Main.Objects.Worlds.SelectWrold;
using Client.Main.Scenes;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Core.Utilities;

namespace Client.Main.Worlds
{

    public class SelectWorld : WorldControl
    {
        private readonly List<PlayerObject> _characterObjects = new();
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _characterInfos = new();
        private readonly Dictionary<PlayerObject, LabelControl> _characterLabels = new();
        private readonly Vector3 _characterDisplayPosition = new(14000, 12295, 250);
        private readonly Vector3 _characterDisplayAngle = new(0, 0, MathHelper.ToRadians(90));
        private ILogger<SelectWorld> _logger;
        private int _currentCharacterIndex = -1;

        // Random animation system for character selection
        private readonly Random _animationRandom = new Random();

        public SelectWorld() : base(worldIndex: 94)
        {
            EnableShadows = false;
            _logger = MuGame.AppLoggerFactory?.CreateLogger<SelectWorld>() ?? throw new InvalidOperationException("LoggerFactory not initialized in MuGame");
            Camera.Instance.ViewFar = 5500f;
        }

        public int CharacterCount => _characterObjects.Count;

        public int CurrentCharacterIndex => _currentCharacterIndex;

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[14] = null;
            MapTileObjects[71] = typeof(BlendedObjects);
            MapTileObjects[11] = typeof(BlendedObjects);
            MapTileObjects[36] = typeof(LightObject);
            MapTileObjects[25] = typeof(BlendedObjects);
            MapTileObjects[33] = typeof(BlendedObjects);
            MapTileObjects[30] = typeof(BlendedObjects);
            MapTileObjects[31] = typeof(FlowersObject2);
            MapTileObjects[34] = typeof(FlowersObject);
            MapTileObjects[26] = typeof(WaterFallObject);
            MapTileObjects[24] = typeof(WaterFallObject);
            MapTileObjects[54] = typeof(WaterSplashObject);
            MapTileObjects[55] = typeof(WaterSplashObject);
            MapTileObjects[56] = typeof(WaterSplashObject);
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            // water animation parameters
            Terrain.WaterSpeed = 0.05f;
            Terrain.DistortionAmplitude = 0.2f;
            Terrain.DistortionFrequency = 1.0f;

            // TODO: Camera position check
            Camera.Instance.Target = new Vector3(14229.295898f, 12340.358398f, 380);
            Camera.Instance.FOV = 29;
#if ANDROID
            Camera.Instance.FOV *= Constants.ANDROID_FOV_SCALE;
#endif
        }

        public async Task CreateCharacterObjects(List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters)
        {
            _logger.LogInformation("Creating {Count} character objects...", characters.Count);

            foreach (var old in _characterObjects)
            {
                old.Click -= PlayerObject_Click;
                Objects.Remove(old);
                old.Dispose();
            }
            _characterObjects.Clear();

            foreach (var lbl in _characterLabels.Values)
            {
                Scene?.Controls.Remove(lbl);
                lbl.Dispose();
            }
            _characterLabels.Clear();

            _characterInfos.Clear();
            _characterInfos.AddRange(characters);
            _currentCharacterIndex = -1;

            if (characters.Count == 0)
            {
                _logger.LogInformation("No characters provided for selection.");
                return;
            }

            var loading = new List<Task>(characters.Count);

            foreach (var (name, cls, lvl, appearanceBytes) in characters)
            {
                var player = new PlayerObject(new AppearanceData(appearanceBytes))
                {
                    Name = name,
                    CharacterClass = cls,
                    Position = _characterDisplayPosition,
                    Angle = _characterDisplayAngle,
                    Interactive = false,
                    World = this,
                    CurrentAction = PlayerAction.PlayerStopMale,
                    Hidden = true
                };

                player.BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 180));

                player.Click += PlayerObject_Click;

                _characterObjects.Add(player);
                Objects.Add(player);
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

                _characterLabels.Add(player, label);
                Scene?.Controls.Add(label);
                label.BringToFront();
            }

            await Task.WhenAll(loading);

            Scene?.Cursor?.BringToFront();
            Scene?.Controls.OfType<LabelControl>()
                   .FirstOrDefault(l => l.Text.StartsWith("Select"))?
                   .BringToFront();

            _logger.LogInformation("Finished creating and loading character objects and labels.");

            SetActiveCharacter(0);
        }

        // *** ADD GETTER FOR LABELS (used by Scene) ***
        public Dictionary<PlayerObject, LabelControl> GetCharacterLabels() => _characterLabels;

        public void SetActiveCharacter(int index)
        {
            if (_characterObjects.Count == 0)
            {
                _currentCharacterIndex = -1;
                return;
            }

            if (index < 0 || index >= _characterObjects.Count)
            {
                _logger.LogWarning("Attempted to activate character at invalid index {Index}", index);
                return;
            }

            if (_currentCharacterIndex == index)
            {
                return;
            }

            for (int i = 0; i < _characterObjects.Count; i++)
            {
                var player = _characterObjects[i];
                bool isActive = i == index;

                player.Hidden = !isActive;
                player.Interactive = isActive;

                if (isActive)
                {
                    if (player.Position != _characterDisplayPosition)
                        player.Position = _characterDisplayPosition;
                    if (player.Angle != _characterDisplayAngle)
                        player.Angle = _characterDisplayAngle;
                }

                if (_characterLabels.TryGetValue(player, out var label))
                {
                    label.Visible = isActive;
                }
            }

            _currentCharacterIndex = index;

            // Play a random emote animation when character is selected
            var activePlayer = _characterObjects[index];
            if (activePlayer != null && !activePlayer.Hidden)
            {
                PlayRandomEmoteForActiveCharacter();
            }
        }

        private void PlayRandomEmoteForActiveCharacter()
        {
            if (_currentCharacterIndex < 0 || _currentCharacterIndex >= _characterObjects.Count)
                return;

            var activePlayer = _characterObjects[_currentCharacterIndex];
            if (activePlayer == null || activePlayer.Hidden)
                return;

            // Check if character is already playing an animation
            if (activePlayer.IsOneShotPlaying)
                return;

            // Define available emote animations based on gender
            bool isFemale = PlayerActionMapper.IsCharacterFemale(activePlayer.CharacterClass);
            var availableEmotes = isFemale
                ? new[] { PlayerAction.PlayerSeeFemale1, PlayerAction.PlayerWinFemale1, PlayerAction.PlayerSmileFemale1 }
                : new[] { PlayerAction.PlayerSee1, PlayerAction.PlayerWin1, PlayerAction.PlayerSmile1 };

            // Select random emote
            var randomEmote = availableEmotes[_animationRandom.Next(availableEmotes.Length)];

            _logger.LogDebug("Playing random emote {Emote} for character {CharacterName} (Female: {IsFemale})",
                randomEmote, activePlayer.Name, isFemale);

            // Play the animation using the new method
            activePlayer.PlayEmoteAnimation(randomEmote);
        }


        private void PlayerObject_Click(object sender, EventArgs e)
        {
            if (sender is PlayerObject clickedPlayer && Scene is SelectCharacterScene selectScene)
            {
                if (_currentCharacterIndex < 0 || _characterObjects[_currentCharacterIndex] != clickedPlayer)
                {
                    _logger.LogDebug("Ignoring click on inactive character '{Name}'.", clickedPlayer.Name);
                    return;
                }
                _logger.LogInformation("PlayerObject '{Name}' clicked.", clickedPlayer.Name);
                selectScene.CharacterSelected(clickedPlayer.Name);
            }
            else if (sender is ModelObject bodyPart && bodyPart.Parent is PlayerObject parentPlayer && Scene is SelectCharacterScene parentScene)
            {
                if (_currentCharacterIndex < 0 || _characterObjects[_currentCharacterIndex] != parentPlayer)
                {
                    _logger.LogDebug("Ignoring click on inactive body part of '{Name}'.", parentPlayer.Name);
                    return;
                }
                _logger.LogInformation("Body part of '{Name}' clicked.", parentPlayer.Name);
                parentScene.CharacterSelected(parentPlayer.Name);
            }
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (!Visible) return;

            if (Status == GameControlStatus.Ready && _characterLabels.Count > 0)
            {
                foreach (var (player, label) in _characterLabels)
                {
                    if (player.Status != GameControlStatus.Ready || !player.Visible)
                    {
                        label.Visible = false;
                        continue;
                    }

                    var head = new Vector3(
                        player.WorldPosition.Translation.X,
                        player.WorldPosition.Translation.Y,
                        player.BoundingBoxWorld.Min.Z - 20);

                    var sp = GraphicsDevice.Viewport.Project(
                                 head,
                                 Camera.Instance.Projection,
                                 Camera.Instance.View,
                                 Matrix.Identity);

                    // Projected coordinates are already in the correct space
                    if (sp.Z is < 0 or > 1)
                    {
                        label.Visible = false;
                        continue;
                    }

                    var font = GraphicsManager.Instance.Font;
                    float k = label.FontSize / Constants.BASE_FONT_SIZE; // Remove RENDER_SCALE - UI system handles this
                    Vector2 s = font.MeasureString(label.Text) * k;

                    // Convert screen coordinates to virtual coordinates for UI system
                    var virtualPos = UiScaler.ToVirtual(new Point((int)sp.X, (int)sp.Y));

                    label.X = (int)(virtualPos.X - s.X / 2f);
                    label.Y = (int)(virtualPos.Y - s.Y - 4);
                    label.ControlSize = new Point((int)s.X, (int)s.Y);
                    label.Visible = true;
                }
            }

            if (MuGame.Instance.PrevKeyboard.IsKeyDown(Keys.Delete) && MuGame.Instance.Keyboard.IsKeyUp(Keys.Delete))
            {
                if (Objects.Count > 0)
                {
                    var obj = Objects[0];
                    _logger?.LogDebug($"Removing obj: {obj.Type} -> {obj.ObjectName}");
                    Objects.RemoveAt(0);
                }
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Add))
            {
                Camera.Instance.ViewFar += 10;
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Subtract))
            {
                Camera.Instance.ViewFar -= 10;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            // Ensure correct render states for DirectX (and OpenGL for consistency)
            var gd = GraphicsManager.Instance.GraphicsDevice;
            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.Default;
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            // This prevents state leakage and texture corruption, especially on DirectX

            base.Draw(gameTime);
        }

        // Keep existing DrawAfter if needed, otherwise remove if labels are handled by base Draw
        // public override void DrawAfter(GameTime gameTime)
        // {
        //     base.DrawAfter(gameTime);
        //     // Labels are now part of Controls, so base.DrawAfter handles them if they implement DrawAfter.
        //     // If LabelControl doesn't override DrawAfter, this method might not be needed here.
        // }
    }
}
