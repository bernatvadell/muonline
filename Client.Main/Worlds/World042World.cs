using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World042World : WalkableWorldControl
    {
        public World042World() : base(worldIndex: 42) // BARRACKS (BALGASS BARRACKS)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(33, 80);
            base.AfterLoad();
        }
    }
}
