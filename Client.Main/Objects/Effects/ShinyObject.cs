using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    public class EffectShiny1 : SpriteObject
    {
        public override string TexturePath => "Effect/Shiny01.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }

    public class EffectShiny2 : SpriteObject
    {
        public override string TexturePath => "Effect/Shiny02.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }

    public class EffectShiny3 : SpriteObject
    {
        public override string TexturePath => "Effect/Shiny03.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }

    public class EffectShiny4 : SpriteObject
    {
        public override string TexturePath => "Effect/Shiny04.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }

    public class EffectShiny5 : SpriteObject
    {
        public override string TexturePath => "Effect/Shiny05.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }

    public class EffectEye : SpriteObject
    {
        public override string TexturePath => "Effect/Eye01.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }

    public class EffectRing : SpriteObject
    {
        public override string TexturePath => "Effect/ring.jpg";
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }
}
