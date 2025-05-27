// SelectWorld.cs

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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Worlds
{

    public class SelectWorld : WorldControl
    {
        // Keep existing fields
        private List<PlayerObject> _characterObjects = new List<PlayerObject>();
        private ILogger<SelectWorld> _logger;

        // *** ADD A DICTIONARY TO STORE LABELS ***
        private Dictionary<PlayerObject, LabelControl> _characterLabels = new();

        public SelectWorld() : base(worldIndex: 94)
        {
            _logger = MuGame.AppLoggerFactory?.CreateLogger<SelectWorld>() ?? throw new InvalidOperationException("LoggerFactory not initialized in MuGame");
            Camera.Instance.ViewFar = 5500f;
        }

        protected override void CreateMapTileObjects()
        {
            // ... (existing CreateMapTileObjects logic) ...
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
        }

        // **** CHANGE METHOD SIGNATURE ****
        public async Task CreateCharacterObjects(List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
            _logger.LogInformation("Creating {Count} character objects…", characters.Count);

            /* 1) czyścimy stare postaci i labele */
            foreach (var old in _characterObjects) { Objects.Remove(old); old.Dispose(); }
            _characterObjects.Clear();

            foreach (var lbl in _characterLabels.Values)
            {
                Scene?.Controls.Remove(lbl);   //  <-- z kolekcji sceny
                lbl.Dispose();
            }
            _characterLabels.Clear();

            /* 2) pozycje startowe */
            Vector3[] pos =
            {
                new Vector3(14000, 11995, 250),
                new Vector3(14000, 12295, 250),
                new Vector3(14000, 12595, 250)
            };

            var loading = new List<Task>();

            /* 3) tworzenie postaci i etykiet */
            for (int i = 0; i < characters.Count && i < pos.Length; i++)
            {
                var (name, cls, lvl) = characters[i];

                var player = new PlayerObject
                {
                    Name = name,
                    CharacterClass = cls,
                    Position = pos[i],
                    Angle = new Vector3(0, 0, MathHelper.ToRadians(90)),
                    Interactive = true,
                    World = this,
                    CurrentAction = PlayerAction.StopMale
                };
                player.Click += PlayerObject_Click;

                _characterObjects.Add(player);
                Objects.Add(player);
                loading.Add(player.Load());

                /* ----- LABEL (dodajemy do Scene.Controls !) ----- */
                var label = new LabelControl
                {
                    Text = $"Lv.{lvl}  {name}",
                    FontSize = 14,
                    TextColor = Color.White,
                    HasShadow = true,
                    ShadowColor = Color.Black * 0.8f,
                    ShadowOffset = new Vector2(1, 1),
                    UseManualPosition = true
                };

                _characterLabels.Add(player, label);
                Scene?.Controls.Add(label);        //  <-- TU!
                label.BringToFront();
            }

            await Task.WhenAll(loading);

            Scene?.Cursor?.BringToFront();
            Scene?.Controls.OfType<LabelControl>()
                   .FirstOrDefault(l => l.Text.StartsWith("Select"))?
                   .BringToFront();

            _logger.LogInformation("Finished creating and loading character objects and labels.");
        }

        // *** ADD GETTER FOR LABELS (used by Scene) ***
        public Dictionary<PlayerObject, LabelControl> GetCharacterLabels() => _characterLabels;


        private void PlayerObject_Click(object sender, EventArgs e)
        {
            if (sender is PlayerObject clickedPlayer && Scene is SelectCharacterScene selectScene)
            {
                _logger.LogInformation("PlayerObject '{Name}' clicked.", clickedPlayer.Name);
                selectScene.CharacterSelected(clickedPlayer.Name);
            }
            else if (sender is ModelObject bodyPart && bodyPart.Parent is PlayerObject parentPlayer && Scene is SelectCharacterScene parentScene)
            {
                _logger.LogInformation("Body part of '{Name}' clicked.", parentPlayer.Name);
                parentScene.CharacterSelected(parentPlayer.Name);
            }
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (!Visible) return;

            /* pozycjonowanie etykiet */
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
                        player.BoundingBoxWorld.Max.Z - 140);

                    var sp = GraphicsDevice.Viewport.Project(
                                 head,
                                 Camera.Instance.Projection,
                                 Camera.Instance.View,
                                 Matrix.Identity);

                    if (sp.Z is < 0 or > 1)
                    {
                        label.Visible = false;
                        continue;
                    }

                    /* faktyczny rozmiar tekstu */
                    var font = GraphicsManager.Instance.Font;
                    float k = label.FontSize / Constants.BASE_FONT_SIZE;
                    Vector2 s = font.MeasureString(label.Text) * k;

                    label.X = (int)(sp.X - s.X / 2f);
                    label.Y = (int)(sp.Y - s.Y - 4);
                    label.ControlSize = new Point((int)s.X, (int)s.Y);   // dla GUI
                    label.Visible = true;
                }
            }

            /* debug-keys bez zmian */
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