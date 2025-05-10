// GameScene.cs
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Represents a single on-screen floating text notification.
    /// </summary>
    public class FloatingText : UIControl
    {
        // ──────────────────────────── Fields ────────────────────────────
        public string Text { get; }
        public Color TextColor { get; }

        private readonly SpriteFont _font;
        private readonly Vector2 _rawSize;
        private Vector2 _center;
        private long _jumpsDone;
        private float _alpha = 1f;

        /// <summary>
        /// Timestamp when this instance was created (seconds since game start).
        /// </summary>
        public float CreationTime { get; }

        // ────────────────────────── Tuning Constants ──────────────────────────
        private const float LIFETIME_SECONDS = 60f;   // Total lifespan
        private const float FADE_START_RATIO = 0.80f; // 80% of lifespan, then begin fade-out
        private const float PULSE_FREQ_HZ = 0.5f;  // Breathing/pulsing frequency
        private const float PULSE_MIN_ALPHA = 0.65f; // Minimum alpha during pulse
        private const float JUMP_INTERVAL = 10f;   // Seconds between upward jumps
        private const float JUMP_PIXELS = -25f;  // Jump offset (negative = upward)
        private const float FONT_SCALE = 0.6f;  // Scale relative to original font size

        // ─────────────────────────── Constructors ───────────────────────────
        public FloatingText(string text, Color color, Vector2 spawnCenter, float creationTime)
        {
            Text = text ?? string.Empty;
            TextColor = color;
            _font = GraphicsManager.Instance.Font
                           ?? throw new InvalidOperationException("SpriteFont is missing.");
            CreationTime = creationTime;

            _rawSize = _font.MeasureString(Text);
            ControlSize = (_rawSize * FONT_SCALE).ToPoint();
            ViewSize = ControlSize;

            _center = spawnCenter;
            UpdatePosition();

            Interactive = false;
            Visible = true;
        }

        // ────────────────────────── Public API ───────────────────────────
        /// <summary>
        /// Updates the vertical center for layout in NotificationManager.
        /// </summary>
        internal void SetCenterY(float newCenterY)
        {
            _center.Y = newCenterY;
            UpdatePosition();
        }

        /// <summary>
        /// Moves the text up or down by the given delta.
        /// </summary>
        public void MoveUp(float deltaY)
        {
            _center.Y += deltaY;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            X = (int)(_center.X - ControlSize.X * 0.5f);
            Y = (int)(_center.Y - ControlSize.Y * 0.5f);
        }

        public float ScaledHeight => _rawSize.Y * FONT_SCALE;

        // ─────────────────────────── Overrides ───────────────────────────
        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            // Compute age in seconds
            float age = (float)gameTime.TotalGameTime.TotalSeconds - CreationTime;

            // Hide and dispose after lifespan ends
            if (age >= LIFETIME_SECONDS)
            {
                Visible = false;
                Dispose();
                return;
            }

            // Perform periodic jump
            long targetJumps = (long)(age / JUMP_INTERVAL);
            if (targetJumps > _jumpsDone)
            {
                long jumpsToDo = targetJumps - _jumpsDone;
                _center.Y += jumpsToDo * JUMP_PIXELS;
                _jumpsDone = targetJumps;
                UpdatePosition();
            }

            // Handle pulsing and fade-out
            float fadeStart = LIFETIME_SECONDS * FADE_START_RATIO;
            if (age < fadeStart)
            {
                // Pulsing alpha between min and 1.0
                float phase = age * PULSE_FREQ_HZ * MathHelper.TwoPi;
                _alpha = MathHelper.Lerp(PULSE_MIN_ALPHA, 1f,
                                         (float)(Math.Sin(phase) + 1f) * 0.5f);
            }
            else
            {
                // Fade out from alpha=1 to alpha=0
                float fadeFraction = (age - fadeStart) / (LIFETIME_SECONDS - fadeStart);
                _alpha = MathHelper.Lerp(1f, 0f, fadeFraction);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _alpha <= 0.01f) return;

            var spriteBatch = GraphicsManager.Instance.Sprite;

            Vector2 drawPos = new Vector2(
                _center.X - _rawSize.X * FONT_SCALE * 0.5f,
                _center.Y - _rawSize.Y * FONT_SCALE * 0.5f);

            spriteBatch.DrawString(
                _font,
                Text,
                drawPos,
                TextColor * _alpha,
                0f,
                Vector2.Zero,
                FONT_SCALE,
                SpriteEffects.None,
                0f);
        }
    }
}
