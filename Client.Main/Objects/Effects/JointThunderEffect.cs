using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class JointThunderEffect : SpriteObject
    {
        public override string TexturePath => $"Effect/JointThunder01.jpg";

        public JointThunderEffect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }
}
