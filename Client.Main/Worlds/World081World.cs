using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World081World : WalkableWorldControl
    {
        public World081World() : base(worldIndex: 81) // KARUTAN
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(40, 88);
            base.AfterLoad();
        }
    }
}
