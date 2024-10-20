using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World069World : WalkableWorldControl
    {
        public World069World() : base(worldIndex: 69) // DOPPELGANGER CRYSTALCAVE (CRYSTALS)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(95, 15);
            base.AfterLoad();
        }
    }
}
