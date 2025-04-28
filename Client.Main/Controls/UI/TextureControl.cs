using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class TextureControl : ExtendedUIControl
    {
        public Texture2D Texture { get; protected set; }
        private string _texturePath;
        public Rectangle TextureRectangle { get; set; }
        public virtual Rectangle SourceRectangle => TextureRectangle;

        public new float Alpha { get; set; } = 1f;
        public string TexturePath { get => _texturePath; set { if (_texturePath != value) { _texturePath = value; OnChangeTexturePath(); } } }
        public BlendState BlendState { get; set; } = BlendState.AlphaBlend;

        public override async Task Initialize()
        {
            await TextureLoader.Instance.Prepare(TexturePath);
            await base.Initialize();
        }

        public override async Task Load()
        {
            await LoadTexture();
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible || Texture == null)
                return;

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

            GraphicsManager.Instance.Sprite.Begin(
                blendState: BlendState,
                effect: GraphicsManager.Instance.AlphaTestEffectUI,
                samplerState: SamplerState.PointClamp,
                depthStencilState: DepthStencilState.Default
            );
            GraphicsManager.Instance.Sprite.Draw(Texture, DisplayRectangle, SourceRectangle, Color.White * Alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            GraphicsManager.Instance.Sprite.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }

        private void OnChangeTexturePath()
        {
            Task.Run(() => LoadTexture());
        }

        protected virtual async Task LoadTexture()
        {
            await TextureLoader.Instance.Prepare(TexturePath);
            Texture = TextureLoader.Instance.GetTexture2D(TexturePath);

            if (Texture == null)
                return;

            ControlSize = new Point(Texture.Width, Texture.Height);

            if (TextureRectangle == Rectangle.Empty)
                TextureRectangle = new Rectangle(0, 0, Texture.Width, Texture.Height);
        }
    }
}
