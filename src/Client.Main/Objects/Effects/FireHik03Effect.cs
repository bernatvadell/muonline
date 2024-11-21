using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class FireHik03Effect : SpriteObject
    {
        public override string TexturePath => $"Effect/firehik03.jpg";

        public FireHik03Effect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
            IsTransparent = true;
        }

        public override float Depth
        {
            get => Parent.Depth + Position.Y + Position.Z;
        }
        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None; // TODO correct depth for this effect should be set
            DepthState = DepthStencilState.None;
            base.Draw(gameTime);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }
}
