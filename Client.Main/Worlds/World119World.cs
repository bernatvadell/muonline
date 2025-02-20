using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World119World : WalkableWorldControl
    {
        public World119World() : base(worldIndex: 119) // DEEP DUNGEON 03
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(27, 227);
            base.AfterLoad();
        }
    }
}
