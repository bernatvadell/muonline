using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World114World : WalkableWorldControl
    {
        public World114World() : base(worldIndex: 114) // NIXIES LAKE
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(115, 103);
            base.AfterLoad();
        }
    }
}
