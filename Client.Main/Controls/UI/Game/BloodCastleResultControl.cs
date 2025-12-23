using System;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// Blood Castle result window showing success/failure, exp, zen, and score.
    /// </summary>
    public sealed class BloodCastleResultControl : UIControl
    {
        private const int WINDOW_WIDTH = 400;
        private const int WINDOW_HEIGHT = 280;
        private const float FONT_SCALE_TITLE = 0.50f;
        private const float FONT_SCALE_BODY = 0.38f;

        private static class Theme
        {
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color Success = new(128, 255, 128);
            public static readonly Color Failure = new(255, 128, 128);
            public static readonly Color ExpColor = new(210, 255, 210);
            public static readonly Color ZenColor = new(255, 210, 210);
            public static readonly Color ScoreColor = new(210, 210, 255);
        }

        private static BloodCastleResultControl _instance;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;
        private SpriteFont _font;

        private Rectangle _closeButtonRect;
        private bool _closeHovered;
        private bool _closePressed;
        private bool _mouseCaptured;

        private bool _success;
        private ulong _experience;
        private int _zen;
        private int _score;

        private BloodCastleResultControl()
        {
            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;

            _closeButtonRect = new Rectangle(WINDOW_WIDTH - 38, 10, 26, 22);
        }

        public static BloodCastleResultControl Instance => _instance ??= new BloodCastleResultControl();

        public override async Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            InvalidateStaticSurface();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime);

            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
            {
                CloseWindow();
                return;
            }

            Point mousePos = MuGame.Instance.UiMouseState.Position;
            UpdateHoverStates(mousePos);
            HandleInput(mousePos);

            if (_mouseCaptured && MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed)
            {
                Scene?.SetMouseInputConsumed();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready) return;

            EnsureStaticSurface();

            var sb = GraphicsManager.Instance.Sprite;
            using var scope = new SpriteBatchScope(
                sb,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                GraphicsManager.GetQualityLinearSamplerState(),
                transform: UiScaler.SpriteTransform);

            sb.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
            DrawDynamicContent(sb);
        }

        public override void Dispose()
        {
            base.Dispose();
            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        public void ShowResult(bool success, ulong experience, int zen, int score)
        {
            _success = success;
            _experience = experience;
            _zen = zen;
            _score = score;
            InvalidateStaticSurface();
            Visible = true;
            BringToFront();
        }

        public void CloseWindow()
        {
            Visible = false;
            _mouseCaptured = false;
            _closePressed = false;
        }

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
            {
                return;
            }

            var gd = GraphicsManager.Instance?.GraphicsDevice;
            if (gd == null) return;

            _staticSurface?.Dispose();
            _staticSurface = new RenderTarget2D(gd, ControlSize.X, ControlSize.Y, false, SurfaceFormat.Color, DepthFormat.None);

            var prev = gd.GetRenderTargets();
            gd.SetRenderTarget(_staticSurface);
            gd.Clear(Color.Transparent);

            var sb = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawStaticElements(sb);
            }

            gd.SetRenderTargets(prev);
            _staticSurfaceDirty = false;
        }

        private void InvalidateStaticSurface() => _staticSurfaceDirty = true;

        private void DrawStaticElements(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var windowRect = new Rectangle(0, 0, ControlSize.X, ControlSize.Y);

            // Background
            sb.Draw(pixel, windowRect, Theme.BorderOuter);
            var inner = new Rectangle(2, 2, ControlSize.X - 4, ControlSize.Y - 4);
            UiDrawHelper.DrawVerticalGradient(sb, inner, Theme.BgDark, Theme.BgDarkest);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), Theme.BorderInner * 0.5f);
            UiDrawHelper.DrawCornerAccents(sb, windowRect, Theme.Accent * 0.4f);
        }

        private void DrawDynamicContent(SpriteBatch sb)
        {
            if (_font == null) return;

            var rect = DisplayRectangle;
            float y = rect.Y + 20;

            // Title
            string title = "BLOOD CASTLE RESULT";
            Vector2 titleSize = _font.MeasureString(title) * FONT_SCALE_TITLE;
            float titleX = rect.X + (rect.Width - titleSize.X) / 2f;
            DrawTextWithShadow(sb, _font, title, titleX, y, FONT_SCALE_TITLE, Theme.TextWhite);
            y += titleSize.Y + 20;

            // Success/Failure message
            Color resultColor = _success ? Theme.Success : Theme.Failure;
            string line1 = _success ? "Mission Complete!" : "Mission Failed!";
            string line2 = _success ? "You have successfully protected the Saint." : "The Saint has been slain.";

            Vector2 line1Size = _font.MeasureString(line1) * FONT_SCALE_BODY;
            float line1X = rect.X + (rect.Width - line1Size.X) / 2f;
            DrawTextWithShadow(sb, _font, line1, line1X, y, FONT_SCALE_BODY, resultColor);
            y += line1Size.Y + 4;

            Vector2 line2Size = _font.MeasureString(line2) * FONT_SCALE_BODY;
            float line2X = rect.X + (rect.Width - line2Size.X) / 2f;
            DrawTextWithShadow(sb, _font, line2, line2X, y, FONT_SCALE_BODY, resultColor);
            y += line2Size.Y + 20;

            // Experience
            string expText = $"Experience Gained: {_experience:N0}";
            Vector2 expSize = _font.MeasureString(expText) * FONT_SCALE_BODY;
            float expX = rect.X + (rect.Width - expSize.X) / 2f;
            DrawTextWithShadow(sb, _font, expText, expX, y, FONT_SCALE_BODY, Theme.ExpColor);
            y += expSize.Y + 10;

            // Zen (only on success)
            if (_success)
            {
                string zenText = $"Zen Gained: {_zen:N0}";
                Vector2 zenSize = _font.MeasureString(zenText) * FONT_SCALE_BODY;
                float zenX = rect.X + (rect.Width - zenSize.X) / 2f;
                DrawTextWithShadow(sb, _font, zenText, zenX, y, FONT_SCALE_BODY, Theme.ZenColor);
                y += zenSize.Y + 10;
            }

            // Score
            string scoreText = $"Score: {_score:N0}";
            Vector2 scoreSize = _font.MeasureString(scoreText) * FONT_SCALE_BODY;
            float scoreX = rect.X + (rect.Width - scoreSize.X) / 2f;
            DrawTextWithShadow(sb, _font, scoreText, scoreX, y, FONT_SCALE_BODY, Theme.ScoreColor);

            // Close button
            DrawCloseButton(sb);
        }

        private void DrawCloseButton(SpriteBatch sb)
        {
            Color bg = _closeHovered ? new Color(70, 85, 110, 150) : new Color(12, 15, 20, 240);
            var rect = new Rectangle(
                DisplayRectangle.X + _closeButtonRect.X,
                DisplayRectangle.Y + _closeButtonRect.Y,
                _closeButtonRect.Width,
                _closeButtonRect.Height);

            UiDrawHelper.DrawPanel(sb, rect, bg,
                Theme.BorderInner, Theme.BorderOuter, Theme.BorderInner * 0.3f,
                _closeHovered, _closeHovered ? Theme.Accent * 0.15f : null);

            string label = "X";
            float scale = 0.40f;
            Vector2 size = _font.MeasureString(label) * scale;
            Vector2 pos = new(
                rect.X + (rect.Width - size.X) / 2f,
                rect.Y + (rect.Height - size.Y) / 2f);

            DrawTextWithShadow(sb, _font, label, pos.X, pos.Y, scale, new Color(255, 220, 130));
        }

        private void UpdateHoverStates(Point mousePos)
        {
            var closeRect = new Rectangle(
                DisplayRectangle.X + _closeButtonRect.X,
                DisplayRectangle.Y + _closeButtonRect.Y,
                _closeButtonRect.Width,
                _closeButtonRect.Height);
            _closeHovered = closeRect.Contains(mousePos);
        }

        private void HandleInput(Point mousePos)
        {
            bool leftJustPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed &&
                                   MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Released &&
                                    MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed && _closeHovered)
            {
                _closePressed = true;
                _mouseCaptured = true;
            }

            if (leftJustReleased && _closePressed)
            {
                _closePressed = false;
                if (_closeHovered)
                {
                    CloseWindow();
                }
                _mouseCaptured = false;
            }
        }

        private void DrawTextWithShadow(SpriteBatch sb, SpriteFont font, string text, float x, float y, float scale, Color color)
        {
            sb.DrawString(font, text, new Vector2(x + 1, y + 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
