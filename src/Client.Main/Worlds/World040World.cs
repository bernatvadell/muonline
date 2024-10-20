using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World040World : WalkableWorldControl
    {
        public World040World() : base(worldIndex : 40) // REFINE TOWER
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(76, 151);
            base.AfterLoad();
        }
    }
}
