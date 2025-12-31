using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    public class FlareEffect : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/flare.jpg";

        public FlareEffect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = new Vector3(0.7f, 0.7f, 0.7f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var lumi = (MathF.Sin((float)gameTime.TotalGameTime.TotalMilliseconds * 0.039f) + 1) * 0.2f + 0.6f;
            Light = new Vector3(lumi * 0.7f, lumi * 0.7f, lumi * 0.7f);
        }
    }
}
