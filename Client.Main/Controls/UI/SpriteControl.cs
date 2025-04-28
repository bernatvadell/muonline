using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        public void SetTexture(Texture2D texture)
        {
            Texture = texture; // Assign directly, assuming Texture is protected in base TextureControl
            if (Texture != null)
            {
                // Update view size based on tile dimensions if needed,
                // but OkButton/ServerButton etc., already set this.
                // If the base TextureControl updates ViewSize on texture change, this might be redundant.
                if (TileWidth > 0 && TileHeight > 0)
                {
                    ViewSize = new Point(TileWidth, TileHeight);
                }
                else if (AutoViewSize)
                {
                    ViewSize = new Point(Texture.Width, Texture.Height);
                }
                // Ensure TextureRectangle is valid if needed
                if (TextureRectangle == Rectangle.Empty && Texture != null)
                    TextureRectangle = new Rectangle(0, 0, Texture.Width, Texture.Height);

            }
            else
            {
                // Handle texture being null if necessary
                ViewSize = Point.Zero;
            }
        }

        public override void Update(GameTime gameTime)
        {
            TextureRectangle = new Rectangle(TileOffset.X + TileX * TileWidth, TileOffset.Y + TileY * TileHeight, TileWidth, TileHeight);
            base.Update(gameTime);
        }
    }
}
