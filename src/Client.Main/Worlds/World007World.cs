using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World007World : WalkableWorldControl
    {
        public World007World() : base(worldIndex: 7) // ARENA
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(72, 163);
            base.AfterLoad();
        }
    }
}
