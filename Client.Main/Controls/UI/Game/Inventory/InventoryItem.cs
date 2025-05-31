using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class InventoryItem
    {
        public ItemDefinition Definition { get; private set; }
        public Point  GridPosition { get; set; }
        public int    Durability   { get; set; }
        public int    Level        { get; set; }

        public InventoryItem(ItemDefinition definition, Point gridPosition,
                             int durability = 255, int level = 0)
        {
            Definition   = definition;
            GridPosition = gridPosition;
            Durability   = durability;
            Level        = level;
        }

        public Rectangle GetBounds()
        {
            return new Rectangle(GridPosition.X, GridPosition.Y,
                                 Definition.Width, Definition.Height);
        }
    }
}