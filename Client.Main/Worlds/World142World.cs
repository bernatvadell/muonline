using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World142World : WalkableWorldControl
    {
        public World142World() : base(worldIndex: 142) // TORMENTA ISLAND
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(189, 224);
            base.AfterLoad();
        }
    }
}
