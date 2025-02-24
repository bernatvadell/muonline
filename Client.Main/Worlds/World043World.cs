using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World043World : WalkableWorldControl
    {
        public World043World() : base(worldIndex: 43) // REFUGE (BALGASS RESTING PLACE)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(98, 185);
            base.AfterLoad();
        }
    }
}
