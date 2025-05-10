using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class FireHik01Effect : SpriteObject
    {
        public override string TexturePath => $"Effect/firehik01.jpg";

        public FireHik01Effect()
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

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
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
