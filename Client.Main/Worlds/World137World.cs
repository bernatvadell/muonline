using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World137World : WalkableWorldControl
    {
        public World137World() : base(worldIndex: 137) // BLAZE KETHOTUM (BURNING KETHOTUM)
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(127, 23);
            base.AfterLoad();
        }
    }
}
