using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Game
{
    public class LoadingScreenControl : GameControl
    {
        private SpriteFont _font;
        public string Message { get; set; } = "Loading...";

        public override async Task Load()
        {
            // Ensure the font is loaded (e.g., from GraphicsManager)
            _font = GraphicsManager.Instance.Font;
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            // Access GraphicsDevice through GraphicsManager
            var gd = GraphicsManager.Instance.GraphicsDevice;
            SpriteBatch spriteBatch = GraphicsManager.Instance.Sprite;
            spriteBatch.Begin();

            // Draw a black background covering the entire screen
            spriteBatch.Draw(GraphicsManager.Instance.Pixel,
                new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height),
                Color.Black);

            if (_font != null)
            {
                Vector2 textSize = _font.MeasureString(Message);
                Vector2 position = new Vector2(
                    (gd.Viewport.Width - textSize.X) / 2,
                    (gd.Viewport.Height - textSize.Y) / 2
                );
                spriteBatch.DrawString(_font, Message, position, Color.White);
            }
            spriteBatch.End();
        }
    }
}