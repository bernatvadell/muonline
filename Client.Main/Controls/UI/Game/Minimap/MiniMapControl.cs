using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    public class MiniMapControl : UIControl
    {
        // --- Constants (Adjust as needed) ---
        private const int MAP_DISPLAY_SIZE = 200; // On-screen size of the map view
        private const float MAP_ROTATION_DEGREES = 0f; // C++ uses 45, set to 0 for simpler start
        private const float PLAYER_ICON_ROTATION_OFFSET_DEGREES = -90f; // Adjust if player icon faces wrong way
        private const float ZOOM_STEP = 200f;
        private const float MIN_ZOOM = 600f;
        private const float MAX_ZOOM = 3000f;
        private const int TOOLTIP_HOVER_RADIUS_SQ = 10 * 10; // Squared radius for tooltip hover

        // --- Textures ---
        private Texture2D _texMap;
        private Texture2D _texFrameCorner;
        private Texture2D _texFrameLine;
        private Texture2D _texPlayerIcon;
        private Texture2D _texPortalIcon;
        private Texture2D _texNpcIcon;
        private Texture2D _texExitButton;

        // --- Child Controls ---
        private SpriteControl _playerMarker;
        private SpriteControl _exitButton;
        private LabelControl _tooltipLabel;

        // --- State ---
        private List<MiniMapMarker> _markers = new List<MiniMapMarker>();
        private Dictionary<int, Vector2> _markerScreenPositions = new Dictionary<int, Vector2>(); // Cache screen pos for tooltips
        private float _currentZoom = 2500f; // Represents the size of the map area shown
        private Vector2 _mapTextureSize = Vector2.Zero; // Actual size of the loaded map texture

        // --- References ---
        private GameScene _gameScene; // Reference to the main game scene

        public MiniMapControl(GameScene scene)
        {
            _gameScene = scene ?? throw new ArgumentNullException(nameof(scene));

            // Basic setup
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter; // Set alignment to center
            Margin = Margin.Empty; // Reset margins for centering
            AutoViewSize = false;
            Visible = false;
            Interactive = true;

            ViewSize = new Point(MAP_DISPLAY_SIZE + 35 * 2, MAP_DISPLAY_SIZE + 35 * 2);
            ControlSize = ViewSize;
            CreateChildControls();
        }

        private void CreateChildControls()
        {
            // Player Marker (fixed in the center)
            _playerMarker = new SpriteControl
            {
                Name = "PlayerMarker",
                TexturePath = "Interface/mini_map_ui_cha.tga", // Initial texture path
                TileWidth = 12, // From C++ RenderImage call
                TileHeight = 12,
                BlendState = BlendState.AlphaBlend,
                Interactive = false,
                Visible = true // Always visible when map is visible
            };
            // We don't add player marker to Controls, drawn manually

            // Exit Button
            _exitButton = new SpriteControl
            {
                Name = "ExitButton",
                TexturePath = "Interface/mini_map_ui_cancel.tga", // Initial texture path
                TileWidth = 30, // From C++ SetBtnPos
                TileHeight = 25,
                BlendState = BlendState.AlphaBlend,
                Interactive = true,
                Visible = true
            };
            _exitButton.Click += (s, e) => Hide();
            Controls.Add(_exitButton);

            // Tooltip Label
            _tooltipLabel = new LabelControl
            {
                Name = "Tooltip",
                Visible = false,
                BackgroundColor = Color.Black * 0.7f,
                TextColor = Color.White,
                FontSize = 10f,
                Padding = new Margin { Left = 3, Right = 3, Top = 1, Bottom = 1 },
                BorderColor = Color.Gray,
                BorderThickness = 1,
                UseManualPosition = true // We position it manually
            };
            Controls.Add(_tooltipLabel); // Add so it gets drawn
        }

        public async Task LoadContentForWorld(short worldIndex)
        {
            try
            {
                // 1. Load Map Texture
                string worldFolder = $"World{worldIndex}";
                // C++ uses .ozt but loads .tga - check your TextureLoader logic
                string mapTexturePath = Path.Combine(worldFolder, "mini_map.tga");
                if (!File.Exists(Path.Combine(Constants.DataPath, mapTexturePath)))
                {
                    mapTexturePath = Path.Combine(worldFolder, "mini_map.ozj"); // Try OZJ as fallback
                }
                if (!File.Exists(Path.Combine(Constants.DataPath, mapTexturePath)))
                {
                    mapTexturePath = Path.Combine(worldFolder, "mini_map.ozt"); // Try OZT as fallback
                }


                if (!File.Exists(Path.Combine(Constants.DataPath, mapTexturePath)))
                {
                    Console.WriteLine($"[MiniMap] Map texture not found for World {worldIndex} at '{mapTexturePath}'");
                    _texMap = null; // Indicate failure
                    _mapTextureSize = Vector2.Zero;

                }
                else
                {
                    _texMap = await TextureLoader.Instance.PrepareAndGetTexture(mapTexturePath);
                    _mapTextureSize = _texMap != null ? new Vector2(_texMap.Width, _texMap.Height) : Vector2.Zero;
                }


                // 2. Load UI Textures (Load only once or check if already loaded)
                if (_texFrameCorner == null)
                    _texFrameCorner = await TextureLoader.Instance.PrepareAndGetTexture("Interface/mini_map_ui_corner.tga");
                if (_texFrameLine == null)
                    _texFrameLine = await TextureLoader.Instance.PrepareAndGetTexture("Interface/mini_map_ui_line.jpg");
                if (_texPlayerIcon == null)
                    _texPlayerIcon = await TextureLoader.Instance.PrepareAndGetTexture("Interface/mini_map_ui_cha.tga");
                if (_texPortalIcon == null)
                    _texPortalIcon = await TextureLoader.Instance.PrepareAndGetTexture("Interface/mini_map_ui_portal.tga");
                if (_texNpcIcon == null)
                    _texNpcIcon = await TextureLoader.Instance.PrepareAndGetTexture("Interface/mini_map_ui_npc.tga");
                if (_texExitButton == null)
                    _texExitButton = await TextureLoader.Instance.PrepareAndGetTexture("Interface/mini_map_ui_cancel.tga");

                // 3. Assign textures to controls
                _playerMarker.SetTexture(_texPlayerIcon);
                _exitButton.SetTexture(_texExitButton); // Use SetTexture helper

                // 4. Load Marker Data (Simulated)
                LoadSimulatedMarkerData(worldIndex);

                // 5. Update Layout
                UpdateLayout();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniMap] Error loading content for World {worldIndex}: {ex.Message}");
                _texMap = null; // Ensure map isn't rendered if loading failed
            }
        }

        // Helper to set texture and update view size for SpriteControl
        private void SetTexture(SpriteControl control, Texture2D texture)
        {
            control?.SetTexture(texture); // Assuming SpriteControl has such a method or handles it via TexturePath
            if (control != null && texture != null)
            {
                // Optionally resize based on texture if needed, but buttons have fixed TileWidth/Height
            }
        }

        private void LoadSimulatedMarkerData(short worldIndex)
        {
            _markers.Clear();
            int idCounter = 0;

            // Example Data (Add more worlds as needed)
            switch (worldIndex)
            {
                case 1: // Lorencia
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.NPC, Location = new Vector2(138, 124), Rotation = 0, Name = "NPC Liaman" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.NPC, Location = new Vector2(120, 111), Rotation = 0, Name = "NPC Potion Girl" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.Portal, Location = new Vector2(130, 240), Rotation = 0, Name = "-> Noria" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.Portal, Location = new Vector2(1, 130), Rotation = 0, Name = "-> Devias" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.Portal, Location = new Vector2(143, 4), Rotation = 0, Name = "-> Dungeon" });
                    break;
                case 4: // Noria
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.NPC, Location = new Vector2(173, 125), Rotation = 0, Name = "Elf Lala" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.NPC, Location = new Vector2(195, 124), Rotation = 0, Name = "Eo the Craftsman" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.NPC, Location = new Vector2(171, 104), Rotation = 0, Name = "Charon" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.Portal, Location = new Vector2(192, 244), Rotation = 0, Name = "-> Lorencia" });
                    break;
                case 3: // Devias
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.Portal, Location = new Vector2(228, 243), Rotation = 0, Name = "-> Lorencia" });
                    _markers.Add(new MiniMapMarker { ID = idCounter++, Kind = MiniMapMarkerKind.Portal, Location = new Vector2(3, 19), Rotation = 0, Name = "-> Elbeland" });
                    break;
                    // Add cases for other worlds
            }
        }

        private void UpdateLayout()
        {
            if (GraphicsDevice == null) return;

            // Main minimap area size
            int frameThickness = 10; // Approximate thickness based on C++ UI texture sizes
            int totalWidth = MAP_DISPLAY_SIZE + frameThickness * 2;
            int totalHeight = MAP_DISPLAY_SIZE + frameThickness * 2;

            ViewSize = new Point(totalWidth, totalHeight);
            ControlSize = ViewSize; // Update control size

            // Re-align based on new size (usually top-right)
            AlignControl();

            // Position exit button (e.g., top-right corner of the frame)
            if (_exitButton != null)
            {
                // Position relative to the MiniMapControl's top-right corner
                _exitButton.X = ViewSize.X - _exitButton.ViewSize.X - frameThickness / 2;
                _exitButton.Y = frameThickness / 2;
            }

            // Player marker is drawn centrally, no layout needed here.
            // Tooltip position is dynamic.
        }

        public void Show()
        {
            if (!Visible)
            {
                if (_texMap == null && _gameScene?.World != null)
                {
                    // Attempt to load content if map isn't loaded (e.g., first show)
                    _ = LoadContentForWorld(_gameScene.World.WorldIndex);
                }
                Visible = true;
                BringToFront();
            }
        }

        public void Hide()
        {
            if (Visible)
            {
                Visible = false;
                _tooltipLabel.Visible = false; // Hide tooltip when map closes
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready || _texMap == null)
            {
                _tooltipLabel.Visible = false; // Ensure tooltip is hidden if map is not visible/ready
                return;
            }

            base.Update(gameTime); // Update children (exit button)

            HandleInput();
            UpdateTooltips();
        }

        private void HandleInput()
        {
            // Keyboard
            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) && MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
            {
                Hide();
            }

            // Mouse Wheel for Zoom
            int scrollDelta = MuGame.Instance.Mouse.ScrollWheelValue - MuGame.Instance.PrevMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _currentZoom -= scrollDelta / 120f * ZOOM_STEP; // Divide by 120 (standard wheel delta)
                _currentZoom = Math.Clamp(_currentZoom, MIN_ZOOM, MAX_ZOOM);
            }
        }

        private void UpdateTooltips()
        {
            if (!Visible || !_tooltipLabel.Visible) _tooltipLabel.Visible = false; // Default hide

            Point mousePos = MuGame.Instance.Mouse.Position;

            // Check only if mouse is roughly over the map display area
            Rectangle mapScreenRect = GetMapScreenRect();
            if (!mapScreenRect.Contains(mousePos))
            {
                _tooltipLabel.Visible = false;
                return;
            }


            MiniMapMarker hoveredMarker = null;
            float closestDistSq = TOOLTIP_HOVER_RADIUS_SQ;

            foreach (var kvp in _markerScreenPositions)
            {
                int markerId = kvp.Key;
                Vector2 screenPos = kvp.Value;
                float distSq = Vector2.DistanceSquared(mousePos.ToVector2(), screenPos);

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    hoveredMarker = _markers.FirstOrDefault(m => m.ID == markerId);
                }
            }

            if (hoveredMarker != null)
            {
                _tooltipLabel.Text = hoveredMarker.Name;
                _tooltipLabel.Visible = true;
                // Position tooltip near mouse
                _tooltipLabel.X = mousePos.X + 10;
                _tooltipLabel.Y = mousePos.Y + 10;
                // Ensure tooltip stays on screen (basic check)
                if (_tooltipLabel.X + _tooltipLabel.ViewSize.X > MuGame.Instance.Width)
                    _tooltipLabel.X = mousePos.X - _tooltipLabel.ViewSize.X - 5;
                if (_tooltipLabel.Y + _tooltipLabel.ViewSize.Y > MuGame.Instance.Height)
                    _tooltipLabel.Y = mousePos.Y - _tooltipLabel.ViewSize.Y - 5;

                _tooltipLabel.BringToFront(); // Make sure tooltip is drawn over other map elements
            }
            else
            {
                _tooltipLabel.Visible = false;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready) return;

            var spriteBatch = GraphicsManager.Instance.Sprite;

            // Draw Background (optional semi-transparent overlay)
            // spriteBatch.Begin();
            // spriteBatch.Draw(GraphicsManager.Instance.Pixel, DisplayRectangle, Color.Black * 0.5f);
            // spriteBatch.End();

            // Draw Map Content
            DrawMap(spriteBatch);

            // Draw Player Marker centrally on top of map content
            DrawPlayerMarker(spriteBatch);

            // Draw Frame around the map area
            DrawFrame(spriteBatch);


            // Base draw call will draw children (Exit Button, Tooltip)
            base.Draw(gameTime);
        }

        private Rectangle GetMapScreenRect()
        {
            // Calculate the rectangle on the screen where the map itself is displayed
            // This depends on your frame drawing logic. Assuming frame adds 'frameThickness' padding.
            int frameThickness = 10; // Match UpdateLayout calculation
            return new Rectangle(
                DisplayRectangle.X + frameThickness,
                DisplayRectangle.Y + frameThickness,
                MAP_DISPLAY_SIZE,
                MAP_DISPLAY_SIZE);
        }

        private void DrawMap(SpriteBatch spriteBatch)
        {
            // Safely get the Walker
            if (_texMap == null || !(_gameScene?.World is WalkableWorldControl walkableWorld) || walkableWorld.Walker == null)
            {
                spriteBatch.Begin();
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, GetMapScreenRect(), Color.DarkSlateGray);
                spriteBatch.End();
                return;
            }
            PlayerObject player = (PlayerObject)walkableWorld.Walker;
            Vector2 playerWorldPos = new Vector2(player.Position.X, player.Position.Y);

            // 1. Calculate the player's relative position in the world (0.0 to 1.0)
            float mapWorldSize = Constants.TERRAIN_SIZE * Constants.TERRAIN_SCALE;
            float playerRelX_World = playerWorldPos.X / mapWorldSize; // Relative position along the world's X axis
            float playerRelY_World = playerWorldPos.Y / mapWorldSize; // Relative position along the world's Y axis

            // 2. Calculate the center of the source rectangle on the map texture
            //    Map WORLD Y axis to TEXTURE U axis (horizontal)
            //    Map WORLD X axis to TEXTURE V axis (vertical)
            float sourceCenterX = playerRelY_World * _mapTextureSize.X;
            float sourceCenterY = playerRelX_World * _mapTextureSize.Y;

            // 3. Calculate the size of the source rectangle based on zoom
            float sourceWidth = (_currentZoom / mapWorldSize) * _mapTextureSize.X;
            float sourceHeight = (_currentZoom / mapWorldSize) * _mapTextureSize.Y;

            // 4. Calculate the source rectangle bounds
            Rectangle sourceRect = new Rectangle(
               (int)(sourceCenterX - sourceWidth / 2f),
               (int)(sourceCenterY - sourceHeight / 2f),
               (int)sourceWidth,
               (int)sourceHeight
           );

            // Clamp sourceRect to map texture bounds
            sourceRect.X = Math.Clamp(sourceRect.X, 0, _texMap.Width - 1);
            sourceRect.Y = Math.Clamp(sourceRect.Y, 0, _texMap.Height - 1);
            sourceRect.Width = Math.Clamp(sourceRect.Width, 1, _texMap.Width - sourceRect.X);
            sourceRect.Height = Math.Clamp(sourceRect.Height, 1, _texMap.Height - sourceRect.Y);

            // 5. Define the destination rectangle on the screen
            Rectangle destRect = GetMapScreenRect();

            // 6. Calculate rotation (leaving at 0 for simplicity)
            float rotationRadians = MathHelper.ToRadians(MAP_ROTATION_DEGREES); // Should be 0

            // 7. Draw the map texture
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(
                _texMap,
                new Vector2(destRect.Center.X, destRect.Center.Y), // Destination position (center)
                sourceRect,      // Source rectangle from map texture
                Color.White,
                rotationRadians, // Rotation angle
                new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f), // Origin = center of sourceRect
                new Vector2((float)destRect.Width / sourceRect.Width, (float)destRect.Height / sourceRect.Height), // Scale to fit destRect
                SpriteEffects.None,
                0f               // Layer depth
            );
            spriteBatch.End();

            // 8. Draw Markers (Marker logic must also account for axis swapping!)
            DrawMarkers(spriteBatch, playerWorldPos, sourceRect, destRect, rotationRadians);
        }

        private void DrawMarkers(SpriteBatch spriteBatch, Vector2 playerWorldPos, Rectangle mapSourceRect, Rectangle screenDestRect, float mapRotationRadians)
        {
            if (_texNpcIcon == null || _texPortalIcon == null) return;

            _markerScreenPositions.Clear();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            float mapWorldSize = Constants.TERRAIN_SIZE * Constants.TERRAIN_SCALE; // Add map world size calculation

            foreach (var marker in _markers)
            {
                // 1. Marker's world position
                Vector2 markerWorldPos = marker.Location * Constants.TERRAIN_SCALE;

                // 2. Marker's position on the map texture (using swapped axes)
                //    Map MARKER WORLD Y axis to TEXTURE U axis
                //    Map MARKER WORLD X axis to TEXTURE V axis
                Vector2 markerTexPos = new Vector2(
                     (markerWorldPos.Y / mapWorldSize) * _mapTextureSize.X, // World Y -> Texture U
                     (markerWorldPos.X / mapWorldSize) * _mapTextureSize.Y  // World X -> Texture V
                 );

                // 3. Marker's position relative to the *center* of the map source rectangle (in texture pixels)
                Vector2 sourceCenterTexPos = new Vector2(mapSourceRect.X + mapSourceRect.Width / 2f, mapSourceRect.Y + mapSourceRect.Height / 2f);
                Vector2 relativeTexPos = markerTexPos - sourceCenterTexPos;

                // 4. Scale this relative texture position to match screen
                float scaleX = (float)screenDestRect.Width / mapSourceRect.Width;
                float scaleY = (float)screenDestRect.Height / mapSourceRect.Height;
                Vector2 relativeScreenPos = new Vector2(relativeTexPos.X * scaleX, relativeTexPos.Y * scaleY);

                // 5. Rotate relative screen position (if mapRotationRadians != 0)
                Matrix rotationMatrix = Matrix.CreateRotationZ(-mapRotationRadians);
                Vector2 rotatedRelativeScreenPos = Vector2.Transform(relativeScreenPos, rotationMatrix);

                // 6. Final screen position
                Vector2 finalScreenPos = new Vector2(screenDestRect.Center.X, screenDestRect.Center.Y) + rotatedRelativeScreenPos;

                // 7. Check if within bounds and draw
                if (screenDestRect.Contains(finalScreenPos))
                {
                    Texture2D iconTexture = null;
                    if (marker.Kind == MiniMapMarkerKind.NPC) iconTexture = _texNpcIcon;
                    else if (marker.Kind == MiniMapMarkerKind.Portal) iconTexture = _texPortalIcon;

                    if (iconTexture != null)
                    {
                        // 8. Adjust marker rotation relative to the swapped axes and map rotation
                        //    Marker world rotation is around the Z axis.
                        //    On a map without rotation (rotationRadians=0), world rotation 0 (points +Y) should correspond to +X on the texture.
                        //    World rotation +90 degrees (points +X) should correspond to +Y on the texture.
                        //    It seems we need to add 90 degrees (PI/2) to the marker's rotation.
                        float markerRotation = MathHelper.ToRadians(marker.Rotation) - mapRotationRadians + MathHelper.PiOver2;

                        Vector2 iconOrigin = new Vector2(iconTexture.Width / 2f, iconTexture.Height / 2f);
                        float iconScale = 0.6f;

                        spriteBatch.Draw(
                            iconTexture,
                            finalScreenPos,
                            null,
                            Color.White,
                            markerRotation, // Use the adjusted rotation
                            iconOrigin,
                            iconScale,
                            SpriteEffects.None,
                            0f
                        );
                        _markerScreenPositions[marker.ID] = finalScreenPos;
                    }
                }
            }
            spriteBatch.End();
        }

        private void DrawPlayerMarker(SpriteBatch spriteBatch)
        {
            // Safely get the Walker
            if (_playerMarker == null || _playerMarker.Texture == null || !(_gameScene?.World is WalkableWorldControl walkableWorld) || walkableWorld.Walker == null)
            {
                return;
            }
            PlayerObject player = (PlayerObject)walkableWorld.Walker;

            Rectangle destRect = GetMapScreenRect();
            Vector2 centerPos = new Vector2(destRect.Center.X, destRect.Center.Y);

            // --- Get the player's world rotation ---
            float playerWorldRotationZ = player.Angle.Z;

            // --- Adjust rotation to the map's coordinate system ---
            // World rotation 0 (points +Y) -> +X on texture (0 degrees on screen with mapRotation=0)
            // World rotation +PI/2 (points +X) -> +Y on texture (+90 degrees on screen with mapRotation=0)
            // It seems the player's rotation (around world Z) must be offset by +90 degrees (PI/2),
            // to match the map's screen orientation, plus the constant icon offset.
            float mapRotationRadians = MathHelper.ToRadians(MAP_ROTATION_DEGREES); // Should be 0
            float finalPlayerRotationOnMap = playerWorldRotationZ + MathHelper.PiOver2 - mapRotationRadians + MathHelper.ToRadians(PLAYER_ICON_ROTATION_OFFSET_DEGREES);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(
                _playerMarker.Texture,
                centerPos,
                null,
                Color.White,
                finalPlayerRotationOnMap, // Use the adjusted final rotation
                new Vector2(_playerMarker.Texture.Width / 2f, _playerMarker.Texture.Height / 2f),
                1.0f,
                SpriteEffects.None,
                0f);
            spriteBatch.End();
        }

        private void DrawFrame(SpriteBatch spriteBatch)
        {
            if (_texFrameCorner == null || _texFrameLine == null) return;

            int cornerSize = 35; // Example size, adjust based on texture
            int lineThickness = 6; // Example size

            Rectangle outerRect = DisplayRectangle; // The whole control area
            Rectangle innerRect = GetMapScreenRect(); // The map display area

            int top = outerRect.Y;
            int left = outerRect.X;
            int right = outerRect.Right;
            int bottom = outerRect.Bottom;
            int width = outerRect.Width;
            int height = outerRect.Height;


            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            // Corners (Assuming texture is 35x35)
            spriteBatch.Draw(_texFrameCorner, new Rectangle(left, top, cornerSize, cornerSize), new Rectangle(0, 0, _texFrameCorner.Width, _texFrameCorner.Height), Color.White); // TL
            spriteBatch.Draw(_texFrameCorner, new Rectangle(right - cornerSize, top, cornerSize, cornerSize), new Rectangle(0, 0, _texFrameCorner.Width, _texFrameCorner.Height), Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f); // TR
            spriteBatch.Draw(_texFrameCorner, new Rectangle(left, bottom - cornerSize, cornerSize, cornerSize), new Rectangle(0, 0, _texFrameCorner.Width, _texFrameCorner.Height), Color.White, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f); // BL
            spriteBatch.Draw(_texFrameCorner, new Rectangle(right - cornerSize, bottom - cornerSize, cornerSize, cornerSize), new Rectangle(0, 0, _texFrameCorner.Width, _texFrameCorner.Height), Color.White, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0f); // BR


            // Lines (Assuming line texture is tileable horizontally)
            // Top Line
            spriteBatch.Draw(_texFrameLine, new Rectangle(left + cornerSize, top, width - cornerSize * 2, lineThickness), Color.White);
            // Bottom Line
            spriteBatch.Draw(_texFrameLine, new Rectangle(left + cornerSize, bottom - lineThickness, width - cornerSize * 2, lineThickness), null, Color.White, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
            // Left Line (Requires rotation or a vertical texture) - Using horizontal texture rotated
            spriteBatch.Draw(_texFrameLine, new Rectangle(left + lineThickness, top + cornerSize, height - cornerSize * 2, lineThickness), null, Color.White, MathHelper.PiOver2, new Vector2(0, 0), SpriteEffects.None, 0f);
            // Right Line
            spriteBatch.Draw(_texFrameLine,
                new Rectangle(right, top + cornerSize, height - cornerSize * 2, lineThickness), // Position and size on screen
                null, // Use the entire source texture
                Color.White,
                MathHelper.PiOver2, // Rotation angle
                new Vector2(0, _texFrameLine.Height), // Origin: Bottom-left corner of the source texture
                SpriteEffects.FlipVertically, // Flip vertically
                0f); // Layer depth


            spriteBatch.End();
        }


        public override void Dispose()
        {
            base.Dispose();
        }
    }
}