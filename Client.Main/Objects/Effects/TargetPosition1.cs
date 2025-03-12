using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class TargetPosition1 : SpriteObject
    {
        public override string TexturePath => $"Effect/cursorpin02.jpg";


        public TargetPosition1()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
