using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class TextureControl : GameControl
    {
        private Texture2D _texture;
        private string _texturePath;

        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int OffsetWidth { get; set; }
        public int OffsetHeight { get; set; }

        public override Rectangle Rectangle => new(OffsetX, OffsetY, Width - OffsetWidth, Height - OffsetHeight);

        public string TexturePath { get => _texturePath; set { if (_texturePath != value) { _texturePath = value; OnChangeTexturePath(); } } }
        public BlendState BlendState { get; set; } = BlendState.Opaque;

        public override async Task Initialize(GraphicsDevice graphicsDevice)
        {
            await TextureLoader.Instance.Prepare(TexturePath);
            await base.Initialize(graphicsDevice);
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await LoadTexture();
            await base.Load(graphicsDevice);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Ready || _texture == null)
                return;

            MuGame.Instance.SpriteBatch.Begin(blendState: BlendState);
            MuGame.Instance.SpriteBatch.Draw(_texture, new Vector2(ScreenX, ScreenY), Rectangle, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            MuGame.Instance.SpriteBatch.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }

        private void OnChangeTexturePath()
        {
            Task.Run(() => LoadTexture());
        }

        private async Task LoadTexture()
        {
            await TextureLoader.Instance.Prepare(TexturePath);
            _texture = TextureLoader.Instance.GetTexture2D(TexturePath);
            Width = _texture.Width;
            Height = _texture.Height;
        }
    }
}
