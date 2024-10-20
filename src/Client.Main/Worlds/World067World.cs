using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World067World : WalkableWorldControl
    {
        public World067World() : base(worldIndex: 67) // DOPPELGANGER BLAZEZONE (VULCAN)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(138, 70);
            base.AfterLoad();
        }
    }
}
