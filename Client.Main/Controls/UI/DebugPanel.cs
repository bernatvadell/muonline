using System.Text;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI
{
    public class DebugPanel : UIControl
    {
        private LabelControl _fpsLabel;
        private LabelControl _mousePosLabel;
        private LabelControl _playerCordsLabel;
        private LabelControl _mapTileLabel;
        private LabelControl _effectsStatusLabel;
        private LabelControl _objectCursorLabel;

        private double _updateTimer = 0;
        private const double UPDATE_INTERVAL_MS = 100; // 100ms
        private StringBuilder _sb = new StringBuilder(100);

        public DebugPanel()
        {
            Align = ControlAlign.Top | ControlAlign.Right;
            Margin = new Margin { Top = 10, Right = 10 };

            Padding = new Margin { Top = 15, Left = 15 };

            ControlSize = new Point(210, 140);
            BackgroundColor = Color.Black * 0.6f;
            BorderColor = Color.White * 0.3f;
            BorderThickness = 2;

            var posX = Padding.Left;
            var posY = Padding.Top;
            var labelHeight = 20;

            Controls.Add(_fpsLabel = new LabelControl
            {
                Text = "FPS: {0}    ",
                TextColor = Color.LightGreen,
                X = posX,
                Y = posY
            });

            Controls.Add(_mousePosLabel = new LabelControl
            {
                Text = "Mouse Position - X: {0}, Y:{1}    ",
                TextColor = Color.LightBlue,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_playerCordsLabel = new LabelControl
            {
                Text = "Player Cords - X: {0}, Y:{1}    ",
                TextColor = Color.LightCoral,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_mapTileLabel = new LabelControl
            {
                Text = "MAP Tile - X: {0}, Y:{1}    ",
                TextColor = Color.LightYellow,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_effectsStatusLabel = new LabelControl
            {
                Text = "FXAA: {0} - AlphaRGB:{1}    ",
                TextColor = Color.Yellow,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_objectCursorLabel = new LabelControl
            {
                Text = "Cursor Object: {0}    ",
                TextColor = Color.CadetBlue,
                X = posX,
                Y = posY += labelHeight
            });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible) return;

            _updateTimer += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_updateTimer >= UPDATE_INTERVAL_MS)
            {
                _updateTimer = 0;

                _sb.Clear().Append("FPS: ").Append((int)FPSCounter.Instance.FPS_AVG);
                _fpsLabel.Text = _sb.ToString();

                // Mouse Position
                _sb.Clear().Append("Mouse Position - X: ").Append(MuGame.Instance.Mouse.Position.X)
                   .Append(", Y:").Append(MuGame.Instance.Mouse.Position.Y);
                _mousePosLabel.Text = _sb.ToString();

                // Effects Status
                _sb.Clear().Append("FXAA: ").Append(GraphicsManager.Instance.IsFXAAEnabled ? "ON" : "OFF")
                   .Append(" - AlphaRGB:").Append(GraphicsManager.Instance.IsAlphaRGBEnabled ? "ON" : "OFF");
                _effectsStatusLabel.Text = _sb.ToString();

                // Cursor Object
                _sb.Clear().Append("Cursor Object: ").Append(World?.Scene?.MouseHoverObject != null ? World.Scene.MouseHoverObject.GetType().Name : "N/A");
                _objectCursorLabel.Text = _sb.ToString();


                if (World is WalkableWorldControl walkableWorld && walkableWorld.Walker != null)
                {
                    _playerCordsLabel.Visible = true;
                    _mapTileLabel.Visible = true;

                    // Player Coords
                    _sb.Clear().Append("Player Cords - X: ").Append(walkableWorld.Walker.Location.X)
                       .Append(", Y:").Append(walkableWorld.Walker.Location.Y);
                    _playerCordsLabel.Text = _sb.ToString();

                    // Map Tile
                    _sb.Clear().Append("MAP Tile - X: ").Append(walkableWorld.MouseTileX)
                       .Append(", Y:").Append(walkableWorld.MouseTileY);
                    _mapTileLabel.Text = _sb.ToString();
                }
                else
                {
                    _playerCordsLabel.Visible = false;
                    _mapTileLabel.Visible = false;
                }
            }
        }
    }
}
