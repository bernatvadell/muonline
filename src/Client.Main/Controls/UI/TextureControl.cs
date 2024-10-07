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

        public virtual Rectangle SourceRectangle => new(OffsetX, OffsetY, _texture.Width - OffsetWidth, _texture.Height - OffsetHeight);
        public override Rectangle Rectangle => new(ScreenX, ScreenY, (int)(SourceRectangle.Width * Scale), (int)(SourceRectangle.Height * Scale));

        public string TexturePath { get => _texturePath; set { if (_texturePath != value) { _texturePath = value; OnChangeTexturePath(); } } }
        public BlendState BlendState { get; set; } = BlendState.Opaque;

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
            if (Status != GameControlStatus.Ready || !Visible || _texture == null)
                return;

            MuGame.Instance.SpriteBatch.Begin(blendState: BlendState);
            MuGame.Instance.SpriteBatch.Draw(_texture, Rectangle, SourceRectangle, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 0f);
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
            
            if(_texture == null)
                return;

            Width = _texture.Width;
            Height = _texture.Height;
        }
    }
}
