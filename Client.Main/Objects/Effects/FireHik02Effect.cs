using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class FireHik02Effect : SpriteObject
    {
        public override string TexturePath => $"Effect/firehik02.jpg";

        public FireHik02Effect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
            IsTransparent = true;
            DepthState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = false
            };
        }

        public override void Draw(GameTime gameTime)
        {
        }

        public override void DrawAfter(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
