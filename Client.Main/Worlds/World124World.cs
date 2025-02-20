using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World124World : WalkableWorldControl
    {
        public World124World() : base(worldIndex: 124) // KUBERA MINE
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(235, 121);
            base.AfterLoad();
        }
    }
}
