using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class Spark03Effect : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => $"Effect/Spark03.jpg";

        public Spark03Effect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = new Vector3(0.5f, 0.5f, 0.5f);
            Scale = 1.4f;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
