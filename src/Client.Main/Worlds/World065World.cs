using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World065World : WalkableWorldControl
    {
        public World065World() : base(worldIndex: 65) // DUELARENA
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(100, 120);
            base.AfterLoad();
        }
    }
}
