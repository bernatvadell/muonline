using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class CloudLightEffect : SpriteObject
    {
        public override string TexturePath => $"Effect/cloudLight.jpg";

        public CloudLightEffect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
        }

        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
