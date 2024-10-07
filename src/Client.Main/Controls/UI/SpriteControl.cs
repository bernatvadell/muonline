using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI
{
    public class SpriteControl : TextureControl
    {
        public int ElementWidth { get; set; }
        public int ElementHeight { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public override Rectangle SourceRectangle => new(OffsetX + TileX * ElementWidth, OffsetY + TileY * ElementHeight, ElementWidth, ElementHeight);
    }
}
