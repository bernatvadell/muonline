using System;
using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game.Common
{
    public static class ItemGridRenderHelper
    {
        public static Point GetSlotAtScreenPosition(Rectangle displayRect, Rectangle gridRect, int columns, int rows, int squareWidth, int squareHeight, Point screenPos)
        {
            Point gridOrigin = new(displayRect.X + gridRect.X, displayRect.Y + gridRect.Y);
            int localX = screenPos.X - gridOrigin.X;
            int localY = screenPos.Y - gridOrigin.Y;

            if (localX < 0 || localY < 0) return new Point(-1, -1);

            int slotX = localX / squareWidth;
            int slotY = localY / squareHeight;

            if (slotX < 0 || slotX >= columns || slotY < 0 || slotY >= rows)
                return new Point(-1, -1);

            return new Point(slotX, slotY);
        }

        public static void DrawGridOverlays(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Rectangle displayRect,
            Rectangle gridRect,
            InventoryItem hoveredItem,
            Point hoveredSlot,
            int squareWidth,
            int squareHeight,
            Color slotHover,
            Color accent,
            float alpha)
        {
            if (pixel == null) return;

            Point gridOrigin = new(displayRect.X + gridRect.X, displayRect.Y + gridRect.Y);

            if (hoveredSlot.X >= 0)
            {
                var rect = new Rectangle(
                    gridOrigin.X + hoveredSlot.X * squareWidth,
                    gridOrigin.Y + hoveredSlot.Y * squareHeight,
                    squareWidth, squareHeight);
                spriteBatch.Draw(pixel, rect, slotHover * alpha);
            }

            if (hoveredItem != null)
            {
                for (int y = 0; y < hoveredItem.Definition.Height; y++)
                {
                    for (int x = 0; x < hoveredItem.Definition.Width; x++)
                    {
                        int sx = hoveredItem.GridPosition.X + x;
                        int sy = hoveredItem.GridPosition.Y + y;

                        if (sx == hoveredSlot.X && sy == hoveredSlot.Y)
                            continue;

                        var rect = new Rectangle(
                            gridOrigin.X + sx * squareWidth,
                            gridOrigin.Y + sy * squareHeight,
                            squareWidth, squareHeight);
                        spriteBatch.Draw(pixel, rect, accent * 0.25f * alpha);
                    }
                }
            }
        }

        public static void DrawItemStackCount(SpriteBatch spriteBatch, SpriteFont font, Rectangle rect, int quantity, Color textColor, float alpha)
        {
            if (font == null) return;
            string text = quantity.ToString();
            const float scale = 0.38f;
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.Right - size.X - 2, rect.Y + 2);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    spriteBatch.DrawString(font, text, pos + new Vector2(dx, dy), Color.Black * alpha,
                                          0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.DrawString(font, text, pos, textColor * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public static void DrawItemLevelBadge(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle rect, int level, Func<int, Color> levelColorSelector, Color shadowColor)
        {
            if (font == null || level <= 0 || pixel == null) return;

            string text = $"+{level}";
            const float scale = 0.30f;

            Vector2 textSize = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.X + 2, rect.Bottom - textSize.Y - 2);

            Color levelColor = levelColorSelector(level);

            var bgRect = new Rectangle((int)pos.X - 2, (int)pos.Y - 1, (int)textSize.X + 4, (int)textSize.Y + 2);
            spriteBatch.Draw(pixel, bgRect, shadowColor);

            spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Black * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, levelColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public static void DrawItemPlaceholder(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle rect, InventoryItem item, Color bgColor, Color textColor)
        {
            if (pixel == null) return;

            spriteBatch.Draw(pixel, rect, bgColor);

            if (font != null && item.Definition.Name != null)
            {
                string shortName = item.Definition.Name.Length > 5
                    ? item.Definition.Name[..5] + ".."
                    : item.Definition.Name;

                float scale = 0.24f;
                Vector2 size = font.MeasureString(shortName) * scale;
                Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);

                spriteBatch.DrawString(font, shortName, pos, textColor,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
