using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Helpers
{
    public static class OverheadNameplateRenderer
    {
        private const float BaseNameScale = 0.42f;
        private const float BaseBarWidth = 70f;
        private const float BaseBarHeight = 6f;
        private const float BaseBarOffsetY = 2f;
        private const float BaseBarPadding = 1f;
        private const float BaseNamePaddingX = 4f;
        private const float BaseNamePaddingY = 2f;
        private const float BaseNameBarGap = 2f;
        private const int BarSegments = 8;

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

        public static bool TryProject(BoundingBox bounds, float zOffset, out Vector3 screen)
        {
            Vector3 anchor = new(
                (bounds.Min.X + bounds.Max.X) * 0.5f,
                (bounds.Min.Y + bounds.Max.Y) * 0.5f,
                bounds.Max.Z + zOffset);

            screen = MuGame.Instance.GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            return screen.Z >= 0f && screen.Z <= 1f;
        }

        public static void DrawNameplate(SpriteBatch spriteBatch, SpriteFont font, Vector3 screen, string name, float? healthFraction, float renderScale)
        {
            if (spriteBatch == null || font == null || string.IsNullOrEmpty(name))
                return;

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null)
                return;

            float scale = MathF.Max(renderScale, 0.1f);
            float nameScale = BaseNameScale * scale;
            Vector2 nameSize = font.MeasureString(name) * nameScale;

            float padX = MathF.Max(1f, BaseNamePaddingX * scale);
            float padY = MathF.Max(1f, BaseNamePaddingY * scale);
            bool hasBar = healthFraction.HasValue;
            float barWidth = MathF.Max(BaseBarWidth * scale, nameSize.X + padX * 2f);
            float barHeight = hasBar ? MathF.Max(1f, BaseBarHeight * scale) : 0f;
            float barOffset = MathF.Max(1f, BaseBarOffsetY * scale);
            float barPadding = MathF.Max(1f, BaseBarPadding * scale);
            float nameGap = MathF.Max(1f, BaseNameBarGap * scale);

            float barBottomY = screen.Y - barOffset;
            float barTopY = barBottomY - barHeight;

            float nameBgHeight = nameSize.Y + padY * 2f;
            float nameBgWidth = nameSize.X + padX * 2f;

            int barX = (int)MathF.Round(screen.X - barWidth * 0.5f);
            int barY = (int)MathF.Round(barTopY);
            int barW = Math.Max(1, (int)MathF.Round(barWidth));
            int barH = Math.Max(1, (int)MathF.Round(barHeight));

            int nameX = (int)MathF.Round(screen.X - nameBgWidth * 0.5f);
            float nameBaseY = hasBar ? (barTopY - nameGap) : (screen.Y - barOffset);
            int nameY = (int)MathF.Round(nameBaseY - nameBgHeight);
            int nameW = Math.Max(1, (int)MathF.Round(nameBgWidth));
            int nameH = Math.Max(1, (int)MathF.Round(nameBgHeight));

            Rectangle nameRect = new(nameX, nameY, nameW, nameH);
            Rectangle barRect = new(barX, barY, barW, barH);

            var viewport = spriteBatch.GraphicsDevice.Viewport;
            int minX = Math.Min(nameRect.Left, barRect.Left);
            int maxX = Math.Max(nameRect.Right, barRect.Right);
            int minY = Math.Min(nameRect.Top, barRect.Top);
            int maxY = Math.Max(nameRect.Bottom, barRect.Bottom);
            if (maxX < 0 || minX > viewport.Width || maxY < 0 || minY > viewport.Height)
                return;

            int pad = Math.Max(0, (int)MathF.Round(barPadding));
            int innerWidth = Math.Max(0, barRect.Width - pad * 2);
            int innerHeight = Math.Max(1, barRect.Height - pad * 2);
            float hpFill = Math.Clamp(healthFraction ?? 0f, 0f, 1f);
            int fillWidth = Math.Max(0, (int)MathF.Round(innerWidth * hpFill));

            Rectangle fillRect = new(
                barRect.X + pad,
                barRect.Y + pad,
                fillWidth,
                innerHeight);

            float depth = MathHelper.Clamp(screen.Z, 0f, 1f);
            Vector2 textPos = new(nameRect.X + padX, nameRect.Y + padY);

            spriteBatch.Draw(pixel, nameRect, null, Theme.BgDarkest * 0.85f, 0f, Vector2.Zero, SpriteEffects.None, depth);
            DrawBorder(spriteBatch, pixel, nameRect, Theme.BorderOuter * 0.9f, depth);

            spriteBatch.DrawString(font, name, textPos + new Vector2(1f, 1f), Color.Black * 0.7f, 0f, Vector2.Zero, nameScale, SpriteEffects.None, depth);
            spriteBatch.DrawString(font, name, textPos, Theme.TextWhite, 0f, Vector2.Zero, nameScale, SpriteEffects.None, depth);

            if (hasBar)
            {
                spriteBatch.Draw(pixel, barRect, null, Color.Black * 0.7f, 0f, Vector2.Zero, SpriteEffects.None, depth);
                if (fillRect.Width > 0)
                    spriteBatch.Draw(pixel, fillRect, null, Theme.Danger, 0f, Vector2.Zero, SpriteEffects.None, depth);

                if (innerWidth > 0 && BarSegments > 1)
                {
                    float segmentWidth = innerWidth / (float)BarSegments;
                    for (int i = 1; i < BarSegments; i++)
                    {
                        int x = barRect.X + pad + (int)MathF.Round(segmentWidth * i);
                        spriteBatch.Draw(pixel,
                            new Rectangle(x, barRect.Y + pad, 1, innerHeight),
                            null,
                            Color.Black * 0.35f,
                            0f,
                            Vector2.Zero,
                            SpriteEffects.None,
                            depth);
                    }
                }

                DrawBorder(spriteBatch, pixel, barRect, Color.Black * 0.8f, depth);
                spriteBatch.Draw(pixel,
                    new Rectangle(barRect.X + 1, barRect.Y + 1, Math.Max(0, barRect.Width - 2), 1),
                    null,
                    Theme.BorderHighlight * 0.6f,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    depth);
            }
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, float depth)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);
        }
    }
}
