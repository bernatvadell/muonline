using System;
using System.Collections.Generic;
using Client.Main;
using Client.Main.Controls.UI.Common;
using Client.Main.Controllers;
using Client.Main.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// Dialog for selecting which object to edit/delete when multiple objects are hovered.
    /// </summary>
    public class ObjectSelectionDialog : UIControl
    {
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<ObjectSelectionDialog>();

        private readonly List<WorldObject> _objects;
        private readonly List<ButtonControl> _objectButtons = new();
        private readonly LabelControl _titleLabel;
        private readonly ButtonControl _closeButton;

        public event Action<WorldObject> ObjectSelected;

        public ObjectSelectionDialog(List<WorldObject> objects)
        {
            _objects = objects ?? new List<WorldObject>();

            AutoViewSize = false;
            ControlSize = new Point(300, 200 + _objects.Count * 35);
            ViewSize = ControlSize;
            BackgroundColor = new Color(20, 20, 40, 240);
            BorderColor = new Color(100, 100, 150, 255);
            BorderThickness = 2;
            Interactive = true;
            Visible = false;

            // Title
            _titleLabel = new LabelControl
            {
                Text = $"Select Object ({_objects.Count} found)",
                X = 10,
                Y = 10,
                FontSize = 14f,
                TextColor = Color.White,
                HasShadow = true
            };
            Controls.Add(_titleLabel);

            // Close button
            _closeButton = new ButtonControl
            {
                Text = "X",
                X = ControlSize.X - 40,
                Y = 5,
                ControlSize = new Point(35, 25),
                ViewSize = new Point(35, 25),
                AutoViewSize = false,
                BackgroundColor = new Color(150, 50, 50, 200),
                HoverBackgroundColor = new Color(200, 80, 80, 220),
                PressedBackgroundColor = new Color(120, 40, 40, 220),
                FontSize = 12f,
                TextColor = Color.White
            };
            _closeButton.Click += (s, e) => Hide();
            Controls.Add(_closeButton);

            // Create buttons for each object
            CreateObjectButtons();
        }

        private void CreateObjectButtons()
        {
            int yOffset = 40;
            const int buttonHeight = 30;
            const int buttonSpacing = 5;

            for (int i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                var displayName = obj is ModelObject mo ? mo.GetType().Name : obj.GetType().Name;
                var idDisplay = obj is WalkerObject ? $"Network ID: {obj.NetworkId}" : $"Type ID: {obj.Type}";
                var button = new ButtonControl
                {
                    Text = $"{displayName} ({idDisplay})",
                    X = 10,
                    Y = yOffset,
                    ControlSize = new Point(280, buttonHeight),
                    ViewSize = new Point(280, buttonHeight),
                    AutoViewSize = false,
                    BackgroundColor = new Color(50, 50, 80, 200),
                    HoverBackgroundColor = new Color(80, 80, 120, 220),
                    PressedBackgroundColor = new Color(40, 40, 70, 220),
                    FontSize = 11f,
                    TextColor = Color.White
                };

                int index = i; // Capture for closure
                button.Click += (s, e) =>
                {
                    ObjectSelected?.Invoke(_objects[index]);
                    Hide();
                };

                _objectButtons.Add(button);
                Controls.Add(button);
                yOffset += buttonHeight + buttonSpacing;
            }
        }

        public void ShowAt(int x, int y)
        {
            X = x;
            Y = y;

            // Clamp to screen bounds
            int maxX = UiScaler.VirtualSize.X - ViewSize.X;
            int maxY = UiScaler.VirtualSize.Y - ViewSize.Y;
            X = Math.Clamp(X, 0, maxX);
            Y = Math.Clamp(Y, 0, maxY);

            Visible = true;
            BringToFront();
        }

        public void Hide()
        {
            Visible = false;
        }
    }
}