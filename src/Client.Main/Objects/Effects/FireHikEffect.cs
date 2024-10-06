using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }

    public class FireHik02Effect : SpriteObject
    {
        public override string TexturePath => $"Effect/firehik02.jpg";

        public FireHik02Effect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
        }
    }

    public class FireHik03Effect : SpriteObject
    {
        public override string TexturePath => $"Effect/firehik03.jpg";

        public FireHik03Effect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = true;
            Light = Vector3.One;
        }
    }
}
