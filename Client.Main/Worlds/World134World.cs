using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World134World : WalkableWorldControl
    {
        public World134World() : base(worldIndex: 134) // TEMPLE OF ARNIL (ARENIL TEMPLE)
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(99, 38);
            base.AfterLoad();
        }
    }
}
