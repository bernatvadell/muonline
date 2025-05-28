using Client.Main.Controllers;
using Client.Main.Models; // For GameControlStatus
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks; // For Task

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class PickedItemRenderer : UIControl // Inherits from UIControl for consistency, although it won't be in the hierarchy
    {
        public InventoryItem Item { get; private set; }
        private Texture2D _itemTexture;
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
            _itemTexture = null;
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

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel; // Use default white pixel

            // Draw a rectangle representing the item
            Rectangle destRect = new Rectangle(X, Y, ViewSize.X, ViewSize.Y);
            spriteBatch.Draw(pixel, destRect, Color.DarkGoldenrod * 0.8f); // Color for the picked-up item

            // Draw item name (simplified)
            if (_font != null && !string.IsNullOrEmpty(Item.Definition.Name))
            {
                Vector2 textSize = _font.MeasureString(Item.Definition.Name) * 0.5f; // Small font
                Vector2 textPos = new Vector2(
                    destRect.X + (destRect.Width - textSize.X) / 2,
                    destRect.Y + (destRect.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(_font, Item.Definition.Name, textPos, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            }

            // We don't call base.Draw(gameTime) because we don't have children and don't want the standard background/frame
        }
    }
}