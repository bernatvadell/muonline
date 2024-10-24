using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Client.Main.Controls.UI
{
    public class DebugPanel : UIControl
    {
        private LabelControl _fpsLabel;
        private LabelControl _mousePosLabel;
        private LabelControl _playerCordsLabel;
        private LabelControl _mapTileLabel;
        private LabelControl _effectsStatusLabel;

        public DebugPanel()
        {
            Align = ControlAlign.Top | ControlAlign.Right;
            Margin = new Margin { Top = 10, Right = 10 };

            AutoSize = false;
            Width = 210;
            Height = 120;
            BackgroundColor = Color.Black * 0.6f;
            BorderColor = Color.White * 0.3f;
            BorderThickness = 2;

            var posX = 15;
            var posY = 15;
            var labelHeight = 20;

            Controls.Add(_fpsLabel = new LabelControl
            {
                Text = "FPS: {0}",
                TextColor = Color.LightGreen,
                X = posX,
                Y = posY
            });

            Controls.Add(_mousePosLabel = new LabelControl
            {
                Text = "Mouse Position - X: {0}, Y:{1}",
                TextColor = Color.LightBlue,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_playerCordsLabel = new LabelControl
            {
                Text = "Player Cords - X: {0}, Y:{1}",
                TextColor = Color.LightCoral,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_mapTileLabel = new LabelControl
            {
                Text = "MAP Tile - X: {0}, Y:{1}",
                TextColor = Color.LightYellow,
                X = posX,
                Y = posY += labelHeight
            });

            Controls.Add(_effectsStatusLabel = new LabelControl
            {
                Text = "FXAA: {0} - AlphaRGB:{1}",
                TextColor = Color.Yellow,
                X = posX,
                Y = posY += labelHeight
            });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _fpsLabel.TextArgs = [(int)FPSCounter.Instance.FPS_AVG];
            _mousePosLabel.TextArgs = [MuGame.Instance.Mouse.Position.X, MuGame.Instance.Mouse.Position.Y];
            _effectsStatusLabel.TextArgs = [GraphicsManager.Instance.IsFXAAEnabled ? "ON" : "OFF", GraphicsManager.Instance.IsAlphaRGBEnabled ? "ON" : "OFF"];

            if (World is WalkableWorldControl walkableWorld)
            {
                _playerCordsLabel.Visible = true;
                _playerCordsLabel.TextArgs = [walkableWorld.Walker.Location.X, walkableWorld.Walker.Location.Y];

                _mapTileLabel.Visible = true;
                _mapTileLabel.TextArgs = [walkableWorld.MouseTileX, walkableWorld.MouseTileY];
            }
            else
            {
                _playerCordsLabel.Visible = false;
                _mapTileLabel.Visible = false;
            }
        }
    }
}
