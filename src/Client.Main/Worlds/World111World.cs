using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World111World : WalkableWorldControl
    {
        public World111World() : base(worldIndex: 111) // NARS (AKERON PART)
        {

        }

        public override void AfterLoad()
        {       
            Walker.Location = new Vector2(135, 152);
            base.AfterLoad();
        }
    }
}
