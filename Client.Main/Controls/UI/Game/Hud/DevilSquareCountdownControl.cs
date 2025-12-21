using System;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Common;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MUnique.OpenMU.Network.Packets.ServerToClient;

namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Displays Devil Square countdown messages (start/close) for 30 seconds.
    /// </summary>
    public sealed class DevilSquareCountdownControl : UIControl
    {
        private const int WIDTH = 520;
        private const int HEIGHT = 64;
        private const int HEADER_HEIGHT = 24;
        private const int PANEL_PADDING = 10;
        private const float FONT_SCALE = 0.45f;
        private const float FONT_SCALE_SMALL = 0.36f;
        private const float COUNTDOWN_SECONDS = 30f;

        private static class Theme
        {
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            public static readonly Color SlotBg = new(12, 15, 20, 240);
            public static readonly Color SlotBorder = new(45, 52, 65, 180);
            public static readonly Color SlotHover = new(70, 85, 110, 150);
            public static readonly Color SlotSelected = new(212, 175, 85, 100);

            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private static DevilSquareCountdownControl _instance;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;
        private SpriteFont _font;

        private UpdateMiniGameState.MiniGameTypeState _state = UpdateMiniGameState.MiniGameTypeState.DevilSquareClosed;
        private string _messageTemplate = string.Empty;
        private float _startTimeSeconds;
        private float _latestTotalSeconds;
        private bool _active;

        private Rectangle _panelRect;
        private Rectangle _headerRect;

        private DevilSquareCountdownControl()
        {
            Align = ControlAlign.Bottom | ControlAlign.HorizontalCenter;
            Margin = new Margin { Bottom = 110 };
            AutoViewSize = false;
            ViewSize = new Point(WIDTH, HEIGHT);
            ControlSize = ViewSize;
            Interactive = false;
            Visible = false;
            BackgroundColor = Color.Transparent;
            BorderColor = Color.Transparent;

            BuildLayoutMetrics();
        }

        public static DevilSquareCountdownControl Instance => _instance ??= new DevilSquareCountdownControl();

        public override async System.Threading.Tasks.Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            InvalidateStaticSurface();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _latestTotalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;

            if (!_active)
            {
                Visible = false;
                return;
            }

            float elapsed = _latestTotalSeconds - _startTimeSeconds;
            if (elapsed >= COUNTDOWN_SECONDS)
            {
                _active = false;
                Visible = false;
            }
            else
            {
                Visible = true;
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
            _staticSurface?.Dispose();
            _staticSurface = null;
            base.Dispose();
        }

        public void StartCountdown(UpdateMiniGameState.MiniGameTypeState state)
        {
            _state = state;
            _messageTemplate = state switch
            {
                UpdateMiniGameState.MiniGameTypeState.DevilSquareClosed => "You will enter Devil Square (in {0} seconds).",
                UpdateMiniGameState.MiniGameTypeState.DevilSquareOpened => "The gate of Devil Square will close down in {0} seconds.",
                UpdateMiniGameState.MiniGameTypeState.DevilSquareRunning => "The gate of Devil Square is closing down ({0} seconds remaining).",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(_messageTemplate))
            {
                _active = false;
                Visible = false;
                return;
            }

            _startTimeSeconds = _latestTotalSeconds;
            _active = true;
            Visible = true;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        private void BuildLayoutMetrics()
        {
            _panelRect = new Rectangle(0, 0, ControlSize.X, ControlSize.Y);
            _headerRect = new Rectangle(PANEL_PADDING, 6, ControlSize.X - PANEL_PADDING * 2, HEADER_HEIGHT);
        }

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
                return;

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
            DrawWindowBackground(sb, _panelRect);
            DrawHeader(sb);
        }

        private void DrawWindowBackground(SpriteBatch sb, Rectangle rect)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            sb.Draw(pixel, rect, Theme.BorderOuter);

            var inner = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            UiDrawHelper.DrawVerticalGradient(sb, inner, Theme.BgDark, Theme.BgDarkest);

            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), Theme.BorderInner * 0.5f);
            UiDrawHelper.DrawCornerAccents(sb, rect, Theme.Accent * 0.35f, size: 10, thickness: 2);
        }

        private void DrawHeader(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            UiDrawHelper.DrawPanel(sb, _headerRect, Theme.BgMid,
                Theme.BorderInner, Theme.BorderOuter, Theme.BorderHighlight * 0.3f);

            sb.Draw(pixel, new Rectangle(_headerRect.X + 8, _headerRect.Y + 4, _headerRect.Width - 16, 2), Theme.Accent * 0.7f);
        }

        private void DrawDynamicContent(SpriteBatch sb)
        {
            if (_font == null || string.IsNullOrEmpty(_messageTemplate)) return;

            int remaining = Math.Max(0, (int)Math.Ceiling(COUNTDOWN_SECONDS - (_latestTotalSeconds - _startTimeSeconds)));
            string message = string.Format(_messageTemplate, remaining);

            float scale = FONT_SCALE;
            Vector2 size = _font.MeasureString(message) * scale;

            float x = (ControlSize.X - size.X) / 2f;
            float y = (ControlSize.Y - size.Y) / 2f + 6f;

            Vector2 pos = Translate(new Vector2(x, y));
            DrawTextWithShadow(sb, message, pos.X, pos.Y, scale, Theme.TextWhite);

            string subtitle = remaining <= 10 ? "Hurry!" : string.Empty;
            if (!string.IsNullOrEmpty(subtitle))
            {
                Vector2 subSize = _font.MeasureString(subtitle) * FONT_SCALE_SMALL;
                float subX = (ControlSize.X - subSize.X) / 2f;
                float subY = y + size.Y + 4f;
                Vector2 subPos = Translate(new Vector2(subX, subY));
                DrawTextWithShadow(sb, subtitle, subPos.X, subPos.Y, FONT_SCALE_SMALL, Theme.Warning);
            }
        }

        private void DrawTextWithShadow(SpriteBatch sb, string text, float x, float y, float scale, Color color)
        {
            sb.DrawString(_font, text, new Vector2(x + 1, y + 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private Vector2 Translate(Vector2 pos)
            => new(pos.X + DisplayRectangle.X, pos.Y + DisplayRectangle.Y);
    }
}
