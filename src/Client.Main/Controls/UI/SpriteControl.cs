using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class SpriteControl : TextureControl
    {
        public Point TileOffset { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }

        public SpriteControl()
        {
            AutoViewSize = false;
        }

        protected override async Task LoadTexture()
        {
            await base.LoadTexture();
            ViewSize = new Point(TileWidth, TileHeight);
        }

        public override void Update(GameTime gameTime)
        {
            TextureRectangle = new Rectangle(TileOffset.X + TileX * TileWidth, TileOffset.Y + TileY * TileHeight, TileWidth, TileHeight);
            base.Update(gameTime);
        }
    }
}
