using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World117World : WalkableWorldControl
    {
        public World117World() : base(worldIndex: 117) // DEEP DUNGEON 01
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(122, 132);
            base.AfterLoad();
        }
    }
}
