using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World038World : WalkableWorldControl
    {
        public World038World() : base(worldIndex: 38) // KANTURU (RUINS)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(20, 217);
            base.AfterLoad();
        }
    }
}
