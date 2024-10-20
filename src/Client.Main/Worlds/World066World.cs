using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World066World : WalkableWorldControl
    {
        public World066World() : base(worldIndex: 66) // DOPPELGANGER ICEZONE (SNOW)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(197, 31);
            base.AfterLoad();
        }
    }
}
