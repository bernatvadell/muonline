using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World057World : WalkableWorldControl
    {
        public World057World() : base(worldIndex: 57) // SWAMP OF PEACE (CALMNESS)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(140, 108);
            base.AfterLoad();
        }
    }
}
