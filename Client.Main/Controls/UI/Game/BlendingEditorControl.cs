using System;
using System.Collections.Generic;
using Client.Main;
using Client.Main.Controls.UI.Common;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// In-game editor for adjusting blending properties of ModelObjects.
    /// Allows changing blend states per mesh for debugging/rendering purposes.
    /// </summary>
    public class BlendingEditorControl : UIControl
    {
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<BlendingEditorControl>();

        private ModelObject _targetObject;
        private readonly List<ButtonControl> _meshButtons = new();
        private readonly LabelControl _titleLabel;
        private readonly ButtonControl _closeButton;

        public event Action<ModelObject> ObjectChanged;

        public BlendingEditorControl()
        {
            AutoViewSize = false;
            ControlSize = new Point(400, 500);
            ViewSize = ControlSize;
            BackgroundColor = new Color(20, 20, 40, 240);
            BorderColor = new Color(100, 100, 150, 255);
            BorderThickness = 2;
            Interactive = true;
            Visible = false;

            // Title
            _titleLabel = new LabelControl
            {
                Text = "Blending Editor",
                X = 10,
                Y = 10,
                FontSize = 16f,
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
        }

        /// <summary>
        /// Shows the blending editor for the specified ModelObject.
        /// </summary>
        public void ShowForObject(ModelObject targetObject)
        {
            if (targetObject == null || targetObject.Model?.Meshes == null)
            {
                _logger?.LogWarning("Cannot show blending editor: invalid target object");
                return;
            }

            _targetObject = targetObject;
            var idDisplay = targetObject is WalkerObject ? $"Network ID: {targetObject.NetworkId}" : $"Type ID: {targetObject.Type}";
            _titleLabel.Text = $"Blending Editor - {targetObject.GetType().Name} ({idDisplay})";

            // Clear previous mesh buttons
            foreach (var button in _meshButtons)
            {
                Controls.Remove(button);
            }
            _meshButtons.Clear();

            // Create buttons for each mesh (limit to first 10 for simplicity)
            var meshes = targetObject.Model.Meshes;
            int maxMeshes = Math.Min(meshes.Length, 10);
            int yOffset = 40;
            const int buttonHeight = 35;
            const int buttonSpacing = 5;

            for (int i = 0; i < maxMeshes; i++)
            {
                var meshButton = CreateMeshButton(i, yOffset);
                _meshButtons.Add(meshButton);
                Controls.Add(meshButton);
                yOffset += buttonHeight + buttonSpacing;
            }

            // Position in center of screen
            X = (UiScaler.VirtualSize.X - ViewSize.X) / 2;
            Y = (UiScaler.VirtualSize.Y - ViewSize.Y) / 2;

            Visible = true;
            BringToFront();
        }

        private ButtonControl CreateMeshButton(int meshIndex, int yOffset)
        {
            // Get current blend state - simplified approach
            var blendState = _targetObject.BlendState; // Use object's main blend state for now
            var blendStateName = GetBlendStateName(blendState);

            var button = new ButtonControl
            {
                Text = $"Mesh {meshIndex}: {blendStateName}",
                X = 10,
                Y = yOffset,
                ControlSize = new Point(360, 30),
                ViewSize = new Point(360, 30),
                AutoViewSize = false,
                BackgroundColor = new Color(50, 50, 80, 200),
                HoverBackgroundColor = new Color(80, 80, 120, 220),
                PressedBackgroundColor = new Color(40, 40, 70, 220),
                FontSize = 11f,
                TextColor = Color.White
            };

            button.Click += (s, e) => ShowBlendStateSelector(meshIndex, button);

            return button;
        }

        private void ShowBlendStateSelector(int meshIndex, ButtonControl meshButton)
        {
            // Create a simple popup with blend state options
            var selector = new BlendStateSelectorPopup(meshIndex, meshButton);
            selector.BlendStateSelected += OnBlendStateSelected;
            Controls.Add(selector);
            selector.ShowAt(meshButton.X + 10, meshButton.Y + meshButton.ViewSize.Y + 5);
        }

        private void OnBlendStateSelected(int meshIndex, BlendState newBlendState)
        {
            if (_targetObject == null)
                return;

            // Update the object's blend state
            _logger?.LogDebug($"Changing blend state for mesh {meshIndex} to {GetBlendStateName(newBlendState)}");

            _targetObject.BlendState = newBlendState;

            // Force buffer invalidation to apply changes
            _targetObject.InvalidateBuffers();

            // Update button text
            var button = _meshButtons[meshIndex];
            button.Text = $"Mesh {meshIndex}: {GetBlendStateName(newBlendState)}";

            ObjectChanged?.Invoke(_targetObject);
        }

        private string GetBlendStateName(BlendState blendState)
        {
            if (blendState == BlendState.Opaque) return "Opaque";
            if (blendState == BlendState.AlphaBlend) return "AlphaBlend";
            if (blendState == BlendState.Additive) return "Additive";
            if (blendState == BlendState.NonPremultiplied) return "NonPremultiplied";
            if (blendState == Blendings.Negative) return "Negative";
            if (blendState == Blendings.DarkBlendState) return "DarkBlend";
            if (blendState == Blendings.Alpha) return "Alpha";
            if (blendState == Blendings.ShadowBlend) return "ShadowBlend";
            if (blendState == Blendings.ColorState) return "ColorState";
            if (blendState == Blendings.MultiplyBlend) return "MultiplyBlend";
            if (blendState == Blendings.InverseDestinationBlend) return "InverseDestBlend";

            return "Custom";
        }

        public void Hide()
        {
            Visible = false;
            _targetObject = null;
        }

        /// <summary>
        /// Simple popup for selecting blend states.
        /// </summary>
        private class BlendStateSelectorPopup : UIControl
        {
            private readonly int _meshIndex;
            private readonly ButtonControl _triggerButton;

            public event Action<int, BlendState> BlendStateSelected;

            public BlendStateSelectorPopup(int meshIndex, ButtonControl triggerButton)
            {
                _meshIndex = meshIndex;
                _triggerButton = triggerButton;

                AutoViewSize = false;
                ControlSize = new Point(200, 350);
                ViewSize = ControlSize;
                BackgroundColor = new Color(40, 40, 60, 240);
                BorderColor = new Color(120, 120, 180, 255);
                BorderThickness = 1;
                Interactive = true;
                Visible = false;

                CreateBlendStateButtons();
            }

            private void CreateBlendStateButtons()
            {
                var blendStates = new[]
                {
                    ("Opaque", BlendState.Opaque),
                    ("AlphaBlend", BlendState.AlphaBlend),
                    ("Additive", BlendState.Additive),
                    ("NonPremultiplied", BlendState.NonPremultiplied),
                    ("Negative", Blendings.Negative),
                    ("DarkBlend", Blendings.DarkBlendState),
                    ("Alpha", Blendings.Alpha),
                    ("ShadowBlend", Blendings.ShadowBlend),
                    ("ColorState", Blendings.ColorState),
                    ("MultiplyBlend", Blendings.MultiplyBlend),
                    ("InverseDestBlend", Blendings.InverseDestinationBlend)
                };

                int yOffset = 10;
                const int buttonHeight = 25;
                const int buttonSpacing = 3;

                foreach (var (name, blendState) in blendStates)
                {
                    var button = new ButtonControl
                    {
                        Text = name,
                        X = 10,
                        Y = yOffset,
                        ControlSize = new Point(180, buttonHeight),
                        ViewSize = new Point(180, buttonHeight),
                        AutoViewSize = false,
                        BackgroundColor = new Color(60, 60, 90, 200),
                        HoverBackgroundColor = new Color(90, 90, 130, 220),
                        PressedBackgroundColor = new Color(50, 50, 80, 220),
                        FontSize = 10f,
                        TextColor = Color.White
                    };

                    button.Click += (s, e) =>
                    {
                        BlendStateSelected?.Invoke(_meshIndex, blendState);
                        Visible = false;
                        Parent?.Controls.Remove(this);
                    };

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
        }
    }
}