using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World082World : WalkableWorldControl
    {
        public World082World() : base(worldIndex: 82) // KARUTAN 2
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(163, 17);
            base.AfterLoad();
        }
    }
}
