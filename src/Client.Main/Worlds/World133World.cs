using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World133World : WalkableWorldControl
    {
        public World133World() : base(worldIndex: 133) // RED SMOKE ICARUS
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(130, 116);
            base.AfterLoad();
        }
    }
}
