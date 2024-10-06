using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public class TextureControl : GameControl
    {
        private Texture2D _texture;
        private SpriteBatch _spriteBatch;
        private string _texturePath;

        public string TexturePath { get => _texturePath; set { if (_texturePath != value) { _texturePath = value; OnChangeTexturePath(); } } }
        public Vector2 Position { get; set; }
        public BlendState BlendState { get; set; } = BlendState.Opaque;

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            if (!string.IsNullOrEmpty(TexturePath))
            {
                await TextureLoader.Instance.Prepare(TexturePath);
                _texture = TextureLoader.Instance.GetTexture2D(TexturePath);
            }

            _spriteBatch = new SpriteBatch(graphicsDevice);
            await base.Load(graphicsDevice);
        }

        public override void Draw(GameTime gameTime)
        {
            _spriteBatch.Begin(blendState: BlendState);
            _spriteBatch.Draw(_texture, Position, null, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            _spriteBatch.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }

        private void OnChangeTexturePath()
        {
            Task.Run(async () =>
            {
                await TextureLoader.Instance.Prepare(TexturePath);
                _texture = TextureLoader.Instance.GetTexture2D(TexturePath);
            });
        }
    }
}
