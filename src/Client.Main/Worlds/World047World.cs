using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World047World : WalkableWorldControl
    {
        public World047World() : base(worldIndex: 47) // ILLUSION TEMPLE
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(135, 119);
            base.AfterLoad();
        }
    }
}
