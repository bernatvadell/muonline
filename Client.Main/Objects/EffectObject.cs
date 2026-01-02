using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Main.Objects
{
    public abstract class EffectObject : WorldObject
    {
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }
}
