using Client.Main.Controllers;
using Client.Main.Models; // For GameControlStatus
using Client.Main.Content; // For TextureLoader
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class PickedItemRenderer : UIControl // Inherits from UIControl for consistency, although it won't be in the hierarchy
    {
        public InventoryItem Item { get; private set; }
        private SpriteFont _font;

        // Slot dimensions for scaling the item "icon"
        private const int SquareWidth = InventoryControl.INVENTORY_SQUARE_WIDTH;
        private const int SquareHeight = InventoryControl.INVENTORY_SQUARE_HEIGHT;

        public PickedItemRenderer()
        {
            Visible = false; // Invisible until an item is picked up
            Interactive = false; // Does not need its own mouse interaction
            Status = GameControlStatus.Ready; // Ready immediately
        }

        public void PickUpItem(InventoryItem item)
        {
            Item = item;
            Visible = true;
            // For now, we draw a rectangle
            if (GraphicsManager.Instance != null) // Ensure GraphicsManager is available
            {
                _font = GraphicsManager.Instance.Font; // Use default font
            }
        }

        public void ReleaseItem()
        {
            Item = null;
            Visible = false;
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible || Item == null) return;

            // Position under the cursor, centered
            int widthInPixels = Item.Definition.Width * SquareWidth;
            int heightInPixels = Item.Definition.Height * SquareHeight;

            X = MuGame.Instance.Mouse.X - widthInPixels / 2;
            Y = MuGame.Instance.Mouse.Y - heightInPixels / 2;
            ViewSize = new Point(widthInPixels, heightInPixels); // Size for drawing
            ControlSize = ViewSize;

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Item == null || GraphicsManager.Instance == null) return;
            
            // This method should not be called directly - use Draw(SpriteBatch, GameTime) instead
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (!Visible || Item == null || spriteBatch == null) return;

            Rectangle destRect = new Rectangle(X, Y, ViewSize.X, ViewSize.Y);

            // Try to get the item's texture
            Texture2D itemTexture = null;
            if (!string.IsNullOrEmpty(Item.Definition.TexturePath))
            {
                itemTexture = TextureLoader.Instance.GetTexture2D(Item.Definition.TexturePath);

                if (itemTexture == null && Item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        itemTexture = BmdPreviewRenderer.GetPreview(Item.Definition, ViewSize.X, ViewSize.Y);
                    }
                    catch (Exception)
                    {
                        // Fall back to rectangle if BMD preview fails
                    }
                }
            }

            if (itemTexture != null)
            {
                // Draw the actual item texture with slight transparency to show it's being dragged
                spriteBatch.Draw(itemTexture, destRect, Color.White * 0.9f);
            }
            else
            {
                // Fallback to colored rectangle
                var pixel = GraphicsManager.Instance.Pixel;
                spriteBatch.Draw(pixel, destRect, Color.DarkGoldenrod * 0.8f);
            }

            // Draw quantity for stackable items (BaseDurability = 0 means it's stackable)
            if (Item.Definition.BaseDurability == 0 && Item.Durability > 1 && _font != null)
            {
                string quantityText = Item.Durability.ToString();
                Vector2 textSize = _font.MeasureString(quantityText);
                float textScale = 0.4f; // Small text
                Vector2 scaledTextSize = textSize * textScale;
                
                // Position in upper right corner of the item
                Vector2 textPosition = new Vector2(
                    destRect.Right - scaledTextSize.X - 2, // 2px margin from right edge
                    destRect.Y + 2 // 2px margin from top edge
                );

                // Draw black outline for readability
                Color outlineColor = Color.Black;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx != 0 || dy != 0) // Skip center
                        {
                            spriteBatch.DrawString(_font, quantityText,
                                textPosition + new Vector2(dx, dy),
                                outlineColor, 0f, Vector2.Zero, textScale,
                                SpriteEffects.None, 0f);
                        }
                    }
                }

                // Draw main text in pale yellow
                Color quantityColor = new Color(255, 255, 180); // Pale yellow
                spriteBatch.DrawString(_font, quantityText, textPosition,
                    quantityColor, 0f, Vector2.Zero, textScale,
                    SpriteEffects.None, 0f);
            }
        }
    }
}