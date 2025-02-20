using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World120World : WalkableWorldControl
    {
        public World120World() : base(worldIndex: 120) // DEEP DUNGEON 04
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(126, 117);
            base.AfterLoad();
        }
    }
}
