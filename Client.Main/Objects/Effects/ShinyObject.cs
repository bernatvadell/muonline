using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    public class EffectShiny1 : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/Shiny01.jpg";
    }

    public class EffectShiny2 : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/Shiny02.jpg";
    }

    public class EffectShiny3 : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/Shiny03.jpg";
    }

    public class EffectShiny4 : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/Shiny04.jpg";
    }

    public class EffectShiny5 : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/Shiny05.jpg";
    }

    public class EffectEye : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/Eye01.jpg";
    }

    public class EffectRing : SpriteObject
    {
        public override bool OutOfView => false;
        public override string TexturePath => "Effect/ring.jpg";
    }
}
