using Client.Main.Controllers;
using Client.Main.Helpers;
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
            if (!Visible) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var spriteBatch = GraphicsManager.Instance.Sprite;

            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                spriteBatch.Draw(
                    GraphicsManager.Instance.Pixel,
                    new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height),
                    Color.Black);

                if (_font != null)
                {
                    var text = Message;
                    Vector2 size = _font.MeasureString(text);
                    Vector2 pos = new Vector2(
                        (gd.Viewport.Width - size.X) * 0.5f,
                        (gd.Viewport.Height - size.Y) * 0.5f);

                    spriteBatch.DrawString(_font, text, pos, Color.White);
                }
            }

            base.Draw(gameTime);
        }
    }
}