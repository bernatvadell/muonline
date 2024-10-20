using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World068World : WalkableWorldControl
    {
        public World068World() : base(worldIndex: 68) // DOPPELGANGER UNDERWATER (SEA)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(110, 58);
            base.AfterLoad();
        }
    }
}
