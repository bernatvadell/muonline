using System;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Models;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// Blood Castle time and statistics display (top-right HUD during event).
    /// Shows remaining time and monster kill count.
    /// </summary>
    public sealed class BloodCastleTimeControl : UIControl
    {
        private const int WINDOW_WIDTH = 200;
        private const int WINDOW_HEIGHT = 100;
        private const int TIME_STATE_NORMAL = 1;
        private const int TIME_STATE_URGENT = 2;

        private static class Theme
        {
            public static readonly Color BgDark = new(16, 20, 26, 220);
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color TextNormal = new(255, 150, 0);   // Orange
            public static readonly Color TextUrgent = new(255, 32, 32);   // Red
            public static readonly Color TextWhite = new(240, 240, 245);
        }

        private static BloodCastleTimeControl _instance;

        private SpriteFont _font;

        private int _remainingTimeSeconds;
        private int _timeState = TIME_STATE_NORMAL;
        private int _killedMonsters;
        private int _maxMonsters = 65535; // Default max (means not set)
        private string _timeText = "00:00:00";

        /// <summary>
        /// Indicates if Blood Castle event is currently active (bridge is open).
        /// When true, players can cross the normally-blocked bridge area.
        /// </summary>
        public static bool IsEventActive { get; private set; }

        private BloodCastleTimeControl()
        {
            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = false;
            Visible = false;
            // Position at top-right
            Align = ControlAlign.Top | ControlAlign.Right;
            X = -10; // Offset from right edge
            Y = 10;  // Offset from top edge
        }

        public static BloodCastleTimeControl Instance => _instance ??= new BloodCastleTimeControl();

        public override async Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready) return;

            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            using var scope = new SpriteBatchScope(
                sb,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                GraphicsManager.GetQualityLinearSamplerState(),
                transform: UiScaler.SpriteTransform);

            var rect = DisplayRectangle;

            // Background
            sb.Draw(pixel, rect, Theme.BgDark);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Theme.BorderOuter);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Theme.BorderOuter);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Theme.BorderOuter);
            sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Theme.BorderOuter);

            // Inner border
            sb.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), Theme.BorderInner * 0.5f);

            float scale = 0.35f;
            float scaleBig = 0.50f;
            float y = rect.Y + 10;

            // Monster count (if set)
            if (_maxMonsters != 65535)
            {
                string monsterText = $"Monsters: {_killedMonsters} / {_maxMonsters}";
                Vector2 monsterSize = _font.MeasureString(monsterText) * scale;
                float monsterX = rect.X + (rect.Width - monsterSize.X) / 2f;
                DrawTextWithShadow(sb, _font, monsterText, monsterX, y, scale, Theme.TextNormal);
                y += _font.LineSpacing * scale + 4;
            }

            // "Remaining Time" label
            string label = "Remaining Time";
            Vector2 labelSize = _font.MeasureString(label) * scale;
            float labelX = rect.X + (rect.Width - labelSize.X) / 2f;
            DrawTextWithShadow(sb, _font, label, labelX, y, scale, Theme.TextWhite);
            y += _font.LineSpacing * scale + 6;

            // Time display (use bigger scale for time)
            Color timeColor = _timeState == TIME_STATE_URGENT ? Theme.TextUrgent : Theme.TextNormal;
            Vector2 timeSize = _font.MeasureString(_timeText) * scaleBig;
            float timeX = rect.X + (rect.Width - timeSize.X) / 2f;
            DrawTextWithShadow(sb, _font, _timeText, timeX, y, scaleBig, timeColor);
        }

        public void SetTime(int remainingSeconds)
        {
            _remainingTimeSeconds = remainingSeconds;
            int minutes = _remainingTimeSeconds / 60;
            int seconds = _remainingTimeSeconds % 60;
            int worldTimeSec = (int)(FPSCounter.Instance.WorldTime / 1000.0f) % 60;
            _timeText = $"{minutes:D2}:{seconds:D2}:{worldTimeSec:D2}";

            _timeState = minutes < 5 ? TIME_STATE_URGENT : TIME_STATE_NORMAL;
        }

        public void SetKillMonsterStatus(int killed, int maxKill)
        {
            _killedMonsters = killed;
            _maxMonsters = maxKill;
        }

        public void ShowWindow()
        {
            Visible = true;
            IsEventActive = true; // Open the bridge when event starts
        }

        public void HideWindow()
        {
            Visible = false;
            IsEventActive = false; // Close the bridge when event ends
            _remainingTimeSeconds = 0;
            _killedMonsters = 0;
            _maxMonsters = 65535;
            _timeState = TIME_STATE_NORMAL;
        }

        private void DrawTextWithShadow(SpriteBatch sb, SpriteFont font, string text, float x, float y, float scale, Color color)
        {
            sb.DrawString(font, text, new Vector2(x + 1, y + 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
