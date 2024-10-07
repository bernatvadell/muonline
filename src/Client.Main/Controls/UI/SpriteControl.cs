using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI
{
    public class SpriteControl : TextureControl
    {
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public override Rectangle SourceRectangle => new(OffsetX + TileX * TileWidth, OffsetY + TileY * TileHeight, TileWidth, TileHeight);
    }
}
