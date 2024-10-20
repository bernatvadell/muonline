using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World121World : WalkableWorldControl
    {
        public World121World() : base(worldIndex: 121) // DEEP DUNGEON 05
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(153, 76);
            base.AfterLoad();
        }
    }
}
