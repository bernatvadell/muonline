using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Main.Objects.Player
{
    public sealed class HeroObject : PlayerObject
    {
        public HeroObject(AppearanceData appearance) : base(appearance)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
