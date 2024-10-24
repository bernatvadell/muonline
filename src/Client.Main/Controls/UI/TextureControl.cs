using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class TextureControl : UIControl
    {
        protected Texture2D Texture { get; private set; }
        private string _texturePath;

        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int OffsetWidth { get; set; }
        public int OffsetHeight { get; set; }

        public virtual Rectangle SourceRectangle => new(OffsetX, OffsetY, Width - OffsetWidth, Height - OffsetHeight);

        public string TexturePath { get => _texturePath; set { if (_texturePath != value) { _texturePath = value; OnChangeTexturePath(); } } }
        public BlendState BlendState { get; set; } = BlendState.Opaque;

        public TextureControl()
        {
            AutoSize = false;
        }

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
                effect: BlendState == BlendState.AlphaBlend ? GraphicsManager.Instance.AlphaTestEffectUI : null,
                samplerState: SamplerState.PointClamp, 
                depthStencilState: DepthStencilState.Default
            );
            GraphicsManager.Instance.Sprite.Draw(Texture, ScreenLocation, SourceRectangle, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 0f);
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

            if (AutoSize || Width == 0 || Height == 0)
            {
                Width = Texture.Width - OffsetWidth;
                Height = Texture.Height - OffsetHeight;
            }
        }
    }
}
