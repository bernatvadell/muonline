using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
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
            await base.LoadTexture(); // This loads Texture from TexturePath in TextureControl
            // Only set ViewSize from TileWidth/Height if AutoViewSize is true
            // or if ViewSize wasn't explicitly set to something else (e.g., it's still Point.Zero or matches Tile dimensions).
            // If Texture is null after base.LoadTexture(), TileWidth/Height might be 0, leading to ViewSize(0,0).
            if (Texture != null) // Ensure texture is loaded before using TileWidth/Height which might depend on it if not set manually
            {
                if (TileWidth > 0 && TileHeight > 0)
                {
                    // If ViewSize was explicitly set (e.g., new Point(36,29)) and AutoViewSize is false, respect that.
                    // Otherwise, if AutoViewSize is true, or ViewSize is still default, use tile dimensions.
                    if (AutoViewSize || (ViewSize.X == 0 && ViewSize.Y == 0) || (ViewSize.X == TileWidth && ViewSize.Y == TileHeight))
                    {
                        ViewSize = new Point(TileWidth, TileHeight);
                    }
                }
                // If AutoViewSize is true and TileWidth/Height are not set, TextureControl's base.LoadTexture would have set ViewSize to Texture.Bounds
            }
            else if (AutoViewSize) // No texture, and AutoViewSize is true
            {
                ViewSize = Point.Zero;
            }
        }


        public void SetTexture(Texture2D texture)
        {
            Texture = texture;
            if (Texture != null)
            {
                if (TileWidth > 0 && TileHeight > 0)
                {
                    if (AutoViewSize || (ViewSize.X == 0 && ViewSize.Y == 0) || (ViewSize.X == TileWidth && ViewSize.Y == TileHeight))
                    {
                        ViewSize = new Point(TileWidth, TileHeight);
                    }
                }
                else if (AutoViewSize)
                {
                    ViewSize = new Point(Texture.Width, Texture.Height);
                }
                if (TextureRectangle == Rectangle.Empty && Texture != null)
                    TextureRectangle = new Rectangle(0, 0, Texture.Width, Texture.Height);

            }
            else
            {
                ViewSize = Point.Zero;
            }
        }

        public override void Update(GameTime gameTime)
        {
            // Update SourceRectangle for sprite sheet animation only if TileWidth/Height are valid
            if (TileWidth > 0 && TileHeight > 0)
            {
                TextureRectangle = new Rectangle(TileOffset.X + TileX * TileWidth, TileOffset.Y + TileY * TileHeight, TileWidth, TileHeight);
            }
            else if (Texture != null && TextureRectangle == Rectangle.Empty) // Ensure TextureRectangle is set if not a spritesheet
            {
                TextureRectangle = new Rectangle(0, 0, Texture.Width, Texture.Height);
            }
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible || Texture == null)
                return;

            var blend = BlendState ?? BlendState.NonPremultiplied;

            using (new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                blend,
                SamplerState.PointClamp))
            {
                GraphicsManager.Instance.Sprite.Draw(
                    Texture, DisplayRectangle, SourceRectangle, // SourceRectangle is now updated in Update
                    Color.White * Alpha);
            }

            base.Draw(gameTime);
        }
    }
}