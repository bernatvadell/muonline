using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class SpriteControl : TextureControl
    {
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public override Rectangle SourceRectangle => new(OffsetX + TileX * TileWidth, OffsetY + TileY * TileHeight, TileWidth, TileHeight);


        protected override async Task LoadTexture()
        {
            await base.LoadTexture();
            Width = TileWidth;
            Height = TileHeight;
        }
    }
}
