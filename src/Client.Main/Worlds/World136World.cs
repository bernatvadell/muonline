using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World136World : WalkableWorldControl
    {
        public World136World() : base(worldIndex: 136) // OLD KETHOTUM
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(124, 16);
            base.AfterLoad();
        }
    }
}
