using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Game
{
    public class LoadingScreenControl : GameControl
    {
        private SpriteFont _font;
        private string _pendingMessage = "Loading…";
        public string Message
        {
            get => _pendingMessage;
            set => _pendingMessage = value ?? "Loading…";
        }

        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font;
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            var gd = GraphicsManager.Instance.GraphicsDevice;
            SpriteBatch spriteBatch = GraphicsManager.Instance.Sprite;
            spriteBatch.Begin();

            // full-screen czarne tło
            spriteBatch.Draw(GraphicsManager.Instance.Pixel,
                             new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height),
                             Color.Black);

            // rysuj napis tylko, gdy font już jest
            if (_font != null)
            {
                Vector2 size = _font.MeasureString(Message);
                Vector2 pos = new Vector2((gd.Viewport.Width - size.X) * 0.5f,
                                           (gd.Viewport.Height - size.Y) * 0.5f);
                spriteBatch.DrawString(_font, Message, pos, Color.White);
            }

            spriteBatch.End();
        }
    }
}