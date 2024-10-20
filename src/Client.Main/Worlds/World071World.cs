using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World071World : WalkableWorldControl
    {
        public World071World() : base(worldIndex: 71) // IMPERIAL GUARDIAN (GAION)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(86, 66);
            base.AfterLoad();
        }
    }
}
