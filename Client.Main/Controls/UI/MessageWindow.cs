using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace Client.Main.Controls.UI
{
    public class MessageWindow : DialogControl
    {
        private readonly TextureControl _background;
        private readonly LabelControl _label;
        private readonly OkButton _okButton;

        public string Text
        {
            get => _label.Text;
            set
            {
                _label.Text = value ?? string.Empty;
                AdjustSizeAndLayout();
            }
        }

        private MessageWindow()
        {
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            AutoViewSize = false;
            BorderColor = Color.Gray * 0.7f;
            BorderThickness = 1;
            BackgroundColor = Color.Black * 0.8f;

            _background = new TextureControl
            {
                TexturePath = "Interface/message_back.tga",
                AutoViewSize = false,
                ViewSize = new Point(352, 113),
                BlendState = BlendState.AlphaBlend
            };
            Controls.Add(_background);

            _label = new LabelControl
            {
                FontSize = 14f,
                TextColor = Color.White,
                TextAlign = HorizontalAlign.Center,
            };
            Controls.Add(_label);

            _okButton = new OkButton
            {
                // Align = ControlAlign.HorizontalCenter // This would also work if X were not manually set
            };
            _okButton.Click += (s, e) => Close();
            Controls.Add(_okButton);

            AdjustSizeAndLayout();
        }

        private void AdjustSizeAndLayout()
        {
            if (_label == null || _okButton == null)
                return;

            int minWidth = 200;
            int minHeight = 120;

            int textWidth = _label.ControlSize.X;
            int textHeight = _label.ControlSize.Y;
            int buttonWidth = _okButton.ViewSize.X;
            int buttonHeight = _okButton.ViewSize.Y;

            int requiredWidth = Math.Max(textWidth, buttonWidth) + 40;
            int finalWidth = Math.Max(minWidth, requiredWidth);

            int requiredHeight = textHeight + buttonHeight + 50;
            int finalHeight = Math.Max(minHeight, requiredHeight);

            ControlSize = new Point(finalWidth, finalHeight);
            ViewSize = ControlSize;

            _label.X = (finalWidth - textWidth) / 2;
            _label.Y = 25;

            _okButton.X = (finalWidth - buttonWidth) / 2;
            _okButton.Y = finalHeight - buttonHeight - 20;
        }

        public static MessageWindow Show(string text)
        {
            var scene = MuGame.Instance?.ActiveScene;
            if (scene == null)
            {
                System.Diagnostics.Debug.WriteLine("[MessageWindow.Show] Error: ActiveScene is null.");
                return null;
            }

            foreach (var existingWindow in scene.Controls.OfType<MessageWindow>().ToList())
            {
                existingWindow.Close();
            }

            var window = new MessageWindow { Text = text };
            window.ShowDialog();
            window.BringToFront();
            return window;
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            DrawBackground();
            DrawBorder();

            _label?.Draw(gameTime);
            _okButton?.Draw(gameTime);
        }
    }
}