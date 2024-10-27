using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class TestDialog : DialogControl
    {
        private Texture2D _cornerTopLeftTexture;
        private Texture2D _topLineTexture;
        private Texture2D _cornerTopRightTexture;
        private Texture2D _leftLineTexture;
        private Texture2D _backgroundTexture;
        private Texture2D _rightLineTexture;
        private Texture2D _cornerBottomLeftTexture;
        private Texture2D _bottomLineTexture;
        private Texture2D _cornerBottomRightTexture;

        public override async Task Load()
        {
            await base.Load();

            var windowName = "popupfield";

            _cornerTopLeftTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}01.ozd");
            _topLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}02.ozd");
            _cornerTopRightTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}03.ozd");
            _leftLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}04.ozd");
            _backgroundTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}05.ozd");
            _rightLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}06.ozd");
            _cornerBottomLeftTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}07.ozd");
            _bottomLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}08.ozd");
            _cornerBottomRightTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}09.ozd");
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            var sprite = GraphicsManager.Instance.Sprite;

            sprite.Begin(
                blendState: BlendState.AlphaBlend,
                effect: GraphicsManager.Instance.AlphaTestEffectUI,
                samplerState: SamplerState.PointClamp,
                depthStencilState: DepthStencilState.Default
            );

            sprite.Draw(_cornerTopLeftTexture, new Rectangle(ScreenLocation.X, ScreenLocation.Y, _cornerTopLeftTexture.Width, _cornerTopLeftTexture.Height), Color.White);
            sprite.Draw(_cornerTopRightTexture, new Rectangle(ScreenLocation.X + Width - _cornerTopRightTexture.Width, ScreenLocation.Y, _cornerTopRightTexture.Width, _cornerTopRightTexture.Height), Color.White);
            sprite.Draw(_cornerBottomLeftTexture, new Rectangle(ScreenLocation.X, ScreenLocation.Y + Height - _cornerBottomLeftTexture.Height, _cornerBottomLeftTexture.Width, _cornerBottomLeftTexture.Height), Color.White);
            sprite.Draw(_cornerBottomRightTexture, new Rectangle(ScreenLocation.X + Width - _cornerBottomRightTexture.Width, ScreenLocation.Y + Height - _cornerBottomRightTexture.Height, _cornerBottomRightTexture.Width, _cornerBottomRightTexture.Height), Color.White);
            sprite.Draw(_topLineTexture, new Rectangle(ScreenLocation.X + _cornerTopLeftTexture.Width, ScreenLocation.Y, Width - _cornerTopLeftTexture.Width - _cornerTopRightTexture.Width, _topLineTexture.Height), Color.White);
            sprite.Draw(_bottomLineTexture, new Rectangle(ScreenLocation.X + _cornerBottomLeftTexture.Width, ScreenLocation.Y + Height - _bottomLineTexture.Height, Width - _cornerBottomLeftTexture.Width - _cornerBottomRightTexture.Width, _bottomLineTexture.Height), Color.White);
            sprite.Draw(_leftLineTexture, new Rectangle(ScreenLocation.X, ScreenLocation.Y + _cornerTopLeftTexture.Height, _leftLineTexture.Width, Height - _cornerTopLeftTexture.Height - _cornerBottomLeftTexture.Height), Color.White);
            sprite.Draw(_rightLineTexture, new Rectangle(ScreenLocation.X + Width - _rightLineTexture.Width, ScreenLocation.Y + _cornerTopRightTexture.Height, _rightLineTexture.Width, Height - _cornerTopRightTexture.Height - _cornerBottomRightTexture.Height), Color.White);
            sprite.Draw(_backgroundTexture, new Rectangle(ScreenLocation.X + _leftLineTexture.Width, ScreenLocation.Y + _topLineTexture.Height, Width - _leftLineTexture.Width - _rightLineTexture.Width, Height - _topLineTexture.Height - _bottomLineTexture.Height), Color.White);

            sprite.End();

            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;

            base.Draw(gameTime);
        }
    }
}
