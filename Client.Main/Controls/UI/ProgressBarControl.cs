using System;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI
{
    public class ProgressBarControl : TextureControl
    {
        private const int ProgressBarHeight = 24;
        private const int ProgressBarMargin = 60;
        private const int ProgressBarBottomOffset = 80;

        // Text-based progress (loading screens)
        public float Progress { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string SizeText { get; set; } = string.Empty;
        public string SpeedText { get; set; } = string.Empty;
        public string EtaText { get; set; } = string.Empty;
        public bool ShowStats { get; set; }

        // Texture-based progress (HUD bars)
        public float Percentage { get; set; } = 1f;
        public bool Vertical { get; set; }
        public bool Inverse { get; set; }

        private bool IsTextMode => string.IsNullOrEmpty(TexturePath);

        public ProgressBarControl()
        {
            AutoViewSize = false;
            Interactive = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsTextMode)
                return;

            if (!Visible || Texture == null || Texture.IsDisposed)
            {
                if (ViewSize != Point.Zero) ViewSize = Point.Zero;
                if (TextureRectangle != Rectangle.Empty) TextureRectangle = Rectangle.Empty;
                return;
            }

            float clampedPercentage = MathHelper.Clamp(Percentage, 0f, 1f);
            int sourceWidth = Texture.Width;
            int sourceHeight = Texture.Height;

            if (!Vertical)
            {
                int calculatedWidth = (int)(sourceWidth * clampedPercentage);
                var currentTextureRect = Inverse
                    ? new Rectangle(sourceWidth - calculatedWidth, 0, calculatedWidth, sourceHeight)
                    : new Rectangle(0, 0, calculatedWidth, sourceHeight);

                TextureRectangle = Rectangle.Intersect(currentTextureRect, Texture.Bounds);
                ViewSize = new Point((int)(TextureRectangle.Width * Scale), (int)(sourceHeight * Scale));
            }
            else
            {
                int calculatedHeight = (int)(sourceHeight * clampedPercentage);
                var currentTextureRect = Inverse
                    ? new Rectangle(0, sourceHeight - calculatedHeight, sourceWidth, calculatedHeight)
                    : new Rectangle(0, 0, sourceWidth, calculatedHeight);

                TextureRectangle = Rectangle.Intersect(currentTextureRect, Texture.Bounds);
                ViewSize = new Point((int)(sourceWidth * Scale), (int)(TextureRectangle.Height * Scale));
            }

            ViewSize = new Point(Math.Max(0, ViewSize.X), Math.Max(0, ViewSize.Y));
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            if (IsTextMode)
            {
                DrawTextProgressBar();
                for (int i = 0; i < Controls.Count; i++)
                {
                    Controls[i].Draw(gameTime);
                }
                return;
            }

            base.Draw(gameTime);
        }

        private void DrawTextProgressBar()
        {
            var sprite = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            int screenW = UiScaler.VirtualSize.X;
            int screenH = UiScaler.VirtualSize.Y;

            int barW = screenW - ProgressBarMargin * 2;
            int barX = ProgressBarMargin;
            int barY = screenH - ProgressBarBottomOffset;

            using var scope = new SpriteBatchScope(sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, null, null, UiScaler.SpriteTransform);

            var pixel = GraphicsManager.Instance.Pixel;

            // Background shadow
            DrawRect(sprite, pixel, barX - 3, barY - 3, barW + 6, ProgressBarHeight + 6, new Color(0, 0, 0, 120));

            // Bar background
            DrawRect(sprite, pixel, barX, barY, barW, ProgressBarHeight, new Color(30, 32, 40));
            DrawRect(sprite, pixel, barX, barY, barW, ProgressBarHeight / 2, new Color(38, 40, 50));

            // Progress fill
            float clampedProgress = MathHelper.Clamp(Progress, 0f, 1f);
            if (clampedProgress > 0.001f)
            {
                int fillW = (int)(barW * clampedProgress);
                if (fillW > 2)
                {
                    // Gradient effect
                    DrawRect(sprite, pixel, barX + 1, barY + 1, fillW - 2, ProgressBarHeight - 2, new Color(40, 150, 90));
                    DrawRect(sprite, pixel, barX + 1, barY + 1, fillW - 2, ProgressBarHeight / 2 - 1, new Color(60, 190, 115));

                    // Top shine
                    DrawRect(sprite, pixel, barX + 1, barY + 1, fillW - 2, 3, new Color(255, 255, 255, 35));
                }
            }

            // Border
            DrawBorder(sprite, pixel, barX, barY, barW, ProgressBarHeight, new Color(60, 65, 80), 1);

            if (font == null) return;

            // Percentage
            string pctText = $"{clampedProgress * 100:F0}%";
            DrawTextCentered(sprite, font, pctText, barX + barW / 2, barY + ProgressBarHeight / 2, Color.White, 0.65f);

            // Status above bar
            DrawTextShadow(sprite, font, StatusText, barX, barY - 28, Color.White, 0.6f);

            // Stats below bar (only when ShowStats is true)
            if (ShowStats && !string.IsNullOrEmpty(SizeText))
            {
                int y = barY + ProgressBarHeight + 10;
                var gray = new Color(160, 165, 180);

                // Size (left)
                DrawTextShadow(sprite, font, SizeText, barX, y, gray, 0.5f);

                // Speed (center)
                if (!string.IsNullOrEmpty(SpeedText))
                {
                    DrawTextCentered(sprite, font, SpeedText, barX + barW / 2, y + 5, new Color(90, 170, 240), 0.5f);
                }

                // ETA (right)
                if (!string.IsNullOrEmpty(EtaText))
                {
                    var etaSize = font.MeasureString(EtaText) * 0.5f;
                    DrawTextShadow(sprite, font, EtaText, barX + barW - (int)etaSize.X, y, gray, 0.5f);
                }
            }
        }

        private static void DrawRect(SpriteBatch sprite, Texture2D pixel, int x, int y, int w, int h, Color color)
        {
            if (pixel != null && w > 0 && h > 0)
                sprite.Draw(pixel, new Rectangle(x, y, w, h), color);
        }

        private static void DrawBorder(SpriteBatch sprite, Texture2D pixel, int x, int y, int w, int h, Color color, int t)
        {
            if (pixel == null) return;
            sprite.Draw(pixel, new Rectangle(x, y, w, t), color);
            sprite.Draw(pixel, new Rectangle(x, y + h - t, w, t), color);
            sprite.Draw(pixel, new Rectangle(x, y, t, h), color);
            sprite.Draw(pixel, new Rectangle(x + w - t, y, t, h), color);
        }

        private static void DrawTextShadow(SpriteBatch sprite, SpriteFont font, string text, int x, int y, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;
            var pos = new Vector2(x, y);
            sprite.DrawString(font, text, pos + Vector2.One, Color.Black * 0.7f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sprite.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private static void DrawTextCentered(SpriteBatch sprite, SpriteFont font, string text, int cx, int cy, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(cx - size.X / 2, cy - size.Y / 2);
            sprite.DrawString(font, text, pos + Vector2.One, Color.Black * 0.7f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sprite.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }
}
