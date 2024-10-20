using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World118World : WalkableWorldControl
    {
        public World118World() : base(worldIndex: 118) // DEEP DUNGEON 02
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(142, 55);
            base.AfterLoad();
        }
    }
}
