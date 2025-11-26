using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Common
{
    public static class UiDrawHelper
    {
        public static void DrawVerticalGradient(SpriteBatch spriteBatch, Rectangle rect, Color top, Color bottom)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int steps = Math.Min(rect.Height, 64);
            int stepHeight = Math.Max(1, rect.Height / steps);

            if (steps <= 1 || rect.Height <= 1)
            {
                spriteBatch.Draw(pixel, rect, bottom);
                return;
            }

            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                Color color = Color.Lerp(top, bottom, t);
                int y = rect.Y + i * stepHeight;
                int height = (i == steps - 1) ? rect.Bottom - y : stepHeight;
                spriteBatch.Draw(pixel, new Rectangle(rect.X, y, rect.Width, height), color);
            }
        }

        public static void DrawHorizontalGradient(SpriteBatch spriteBatch, Rectangle rect, Color left, Color right)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int steps = Math.Min(rect.Width, 64);
            int stepWidth = Math.Max(1, rect.Width / steps);

            if (steps <= 1 || rect.Width <= 1)
            {
                spriteBatch.Draw(pixel, rect, right);
                return;
            }

            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                Color color = Color.Lerp(left, right, t);
                int x = rect.X + i * stepWidth;
                int width = (i == steps - 1) ? rect.Right - x : stepWidth;
                spriteBatch.Draw(pixel, new Rectangle(x, rect.Y, width, rect.Height), color);
            }
        }

        public static void DrawCornerAccents(SpriteBatch spriteBatch, Rectangle rect, Color color, int size = 12, int thickness = 2)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, size), color);

            spriteBatch.Draw(pixel, new Rectangle(rect.Right - size, rect.Y, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, size), color);

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - size, thickness, size), color);

            spriteBatch.Draw(pixel, new Rectangle(rect.Right - size, rect.Bottom - thickness, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Bottom - size, thickness, size), color);
        }

        public static void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color bgColor, Color? borderInner = null, Color? borderOuter = null, Color? borderHighlight = null, bool withGlow = false, Color? glowColor = null)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            if (withGlow && glowColor.HasValue)
            {
                var glowRect = new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);
                spriteBatch.Draw(pixel, glowRect, glowColor.Value);
            }

            spriteBatch.Draw(pixel, rect, bgColor);

            if (borderInner.HasValue || borderOuter.HasValue)
            {
                var inner = borderInner ?? Color.White;
                var outer = borderOuter ?? Color.White;

                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), inner);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), outer);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), inner * 0.7f);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), outer);

                if (borderHighlight.HasValue)
                {
                    spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), borderHighlight.Value);
                }
            }
        }
    }
}
