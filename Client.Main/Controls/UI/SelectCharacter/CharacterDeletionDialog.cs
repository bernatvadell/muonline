using Client.Main.Controls.UI.Common;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Dialog for confirming character deletion with security code input.
    /// </summary>
    public class CharacterDeletionDialog : GameControl
    {
        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME - Matching SelectCharacterScene
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            // Background layers
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            // Accent - Warm Gold
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            // Secondary accent - Cool Blue
            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

            // Borders
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            // Text
            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            // Status colors
            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private RenderTarget2D _backgroundSurface;
        private bool _surfaceNeedsRedraw = true;
        private double _bringToFrontTimer = 0;
        private const double BRING_TO_FRONT_INTERVAL = 0.1; // Throttle to every 100ms
        private readonly string _characterName;
        private LabelControl _titleLabel;
        private LabelControl _messageLabel;
        private LabelControl _securityCodeLabel;
        private TextBoxControl _securityCodeInput;
        private ButtonControl _confirmButton;
        private ButtonControl _cancelButton;

        public event EventHandler<string> DeleteConfirmed;
        public event EventHandler CancelRequested;

        public CharacterDeletionDialog(string characterName)
        {
            _characterName = characterName;
            
            AutoViewSize = false;
            ViewSize = new Point(550, 400);
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            Interactive = true;

            InitializeControls();
            
            // Ensure dialog appears on top of character cards
            BringToFront();
        }

        private void InitializeControls()
        {
            // Title
            _titleLabel = new LabelControl
            {
                Text = "DELETE CHARACTER",
                FontSize = 20f,
                TextColor = Theme.Danger,
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                Margin = new Margin { Top = 10 }
            };
            Controls.Add(_titleLabel);

            // Warning message
            _messageLabel = new LabelControl
            {
                Text = $"Are you sure you want to delete '{_characterName}'?\n\nThis action cannot be undone!\n\nEnter your security code to confirm:",
                FontSize = 13f,
                TextColor = Theme.TextWhite,
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                Margin = new Margin { Top = 70 },
                ViewSize = new Point(450, 120),
                X = 50
            };
            Controls.Add(_messageLabel);

            // Security code label
            _securityCodeLabel = new LabelControl
            {
                Text = "Security Code:",
                FontSize = 13f,
                TextColor = Theme.TextWhite,
                X = 50,
                Y = 200
            };
            Controls.Add(_securityCodeLabel);

            // Security code input
            _securityCodeInput = new TextBoxControl
            {
                X = 50,
                Y = 230,
                ViewSize = new Point(450, 36),
                MaxLength = 20,
                PlaceholderText = "Enter security code...",
                FontSize = 8f,
                BackgroundColor = Theme.BgDark,
                TextColor = Theme.TextWhite,
                BorderColor = Theme.BorderInner,
                BorderThickness = 1
            };
            Controls.Add(_securityCodeInput);
            _securityCodeInput.Focus();

            // Delete button
            _confirmButton = CreateModernButton("DELETE CHARACTER", Theme.Danger);
            _confirmButton.X = 50;
            _confirmButton.Y = 300;
            _confirmButton.ViewSize = new Point(220, 40);
            _confirmButton.Click += OnConfirmClick;
            Controls.Add(_confirmButton);

            // Cancel button
            _cancelButton = CreateModernButton("CANCEL", Theme.BgLight);
            _cancelButton.X = 280;
            _cancelButton.Y = 300;
            _cancelButton.ViewSize = new Point(220, 40);
            _cancelButton.Click += OnCancelClick;
            Controls.Add(_cancelButton);
        }

        private ButtonControl CreateModernButton(string text, Color baseColor)
        {
            return new ButtonControl
            {
                Text = text,
                FontSize = 13f,
                AutoViewSize = false,
                BackgroundColor = baseColor,
                HoverBackgroundColor = Color.Lerp(baseColor, Color.White, 0.2f),
                PressedBackgroundColor = Color.Lerp(baseColor, Color.Black, 0.2f),
                TextColor = Theme.TextWhite,
                HoverTextColor = Theme.TextWhite,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                BorderThickness = 1,
                BorderColor = Theme.BorderInner
            };
        }

        private void OnConfirmClick(object sender, EventArgs e)
        {
            string securityCode = _securityCodeInput.Text.Trim();
            DeleteConfirmed?.Invoke(this, securityCode);
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        private void RegenerateBackgroundSurface()
        {
            _backgroundSurface?.Dispose();

            int width = ViewSize.X;
            int height = ViewSize.Y;

            _backgroundSurface = new RenderTarget2D(
                GraphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            // Render background to surface
            var oldTargets = GraphicsDevice.GetRenderTargets();
            GraphicsDevice.SetRenderTarget(_backgroundSurface);
            GraphicsDevice.Clear(Color.Transparent);

            // Create a new SpriteBatch instance to avoid conflicts with shared instance
            using var batch = new SpriteBatch(GraphicsDevice);
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            var pixel = UiDrawHelper.GetPixelTexture(GraphicsDevice);
            var dialogRect = new Rectangle(0, 0, width, height);

            // Main panel background with gradient
            UiDrawHelper.DrawVerticalGradient(batch, dialogRect, Theme.BgMid, Theme.BgDark);

            // Outer border
            batch.Draw(pixel, new Rectangle(0, 0, dialogRect.Width, 1), Theme.BorderOuter);
            batch.Draw(pixel, new Rectangle(0, dialogRect.Height - 1, dialogRect.Width, 1), Theme.BorderOuter);
            batch.Draw(pixel, new Rectangle(0, 0, 1, dialogRect.Height), Theme.BorderOuter);
            batch.Draw(pixel, new Rectangle(dialogRect.Width - 1, 0, 1, dialogRect.Height), Theme.BorderOuter);

            // Header section with danger accent - aligned with dialog (no offset)
            var headerRect = new Rectangle(0, 0, dialogRect.Width, 50);
            UiDrawHelper.DrawHorizontalGradient(batch, headerRect, Theme.BgLighter, Theme.BgMid);
            
            // Danger accent on header (top stripe)
            batch.Draw(pixel, new Rectangle(0, 0, dialogRect.Width, 3), Theme.Danger * 0.6f);
            
            // Corner accents aligned with header
            UiDrawHelper.DrawCornerAccents(batch, headerRect, Theme.Danger, 12, 2);

            // Header separator (aligned with header width)
            batch.Draw(pixel, new Rectangle(0, headerRect.Bottom - 1, headerRect.Width, 1), Theme.BorderInner);
            batch.Draw(pixel, new Rectangle(0, headerRect.Bottom - 2, headerRect.Width, 1), Theme.Danger * 0.3f);

            batch.End();

            GraphicsDevice.SetRenderTargets(oldTargets);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            
            // Ensure dialog stays on top of character cards (throttled)
            _bringToFrontTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_bringToFrontTimer >= BRING_TO_FRONT_INTERVAL && Parent != null)
            {
                _bringToFrontTimer = 0;
                BringToFront();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status == GameControlStatus.Ready)
            {
                if (_backgroundSurface == null || _surfaceNeedsRedraw ||
                    _backgroundSurface.Width != ViewSize.X || _backgroundSurface.Height != ViewSize.Y)
                {
                    RegenerateBackgroundSurface();
                    _surfaceNeedsRedraw = false;
                }

                if (_backgroundSurface != null)
                {
                    var sb = GraphicsManager.Instance.Sprite;
                    using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, UiScaler.SpriteTransform))
                    {
                        var destRect = new Rectangle(
                            DisplayPosition.X,
                            DisplayPosition.Y,
                            ViewSize.X,
                            ViewSize.Y);

                        sb.Draw(_backgroundSurface, destRect, Color.White);
                    }
                }
            }

            base.Draw(gameTime);
        }

        public override void Dispose()
        {
            _backgroundSurface?.Dispose();
            _backgroundSurface = null;

            if (_confirmButton != null)
            {
                _confirmButton.Click -= OnConfirmClick;
            }

            if (_cancelButton != null)
            {
                _cancelButton.Click -= OnCancelClick;
            }

            base.Dispose();
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            _surfaceNeedsRedraw = true;
        }
    }
}
