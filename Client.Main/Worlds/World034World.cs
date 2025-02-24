using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World034World : WalkableWorldControl
    {
        public World034World() : base(worldIndex: 34) // AIDA
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(85, 10);
            base.AfterLoad();
        }
    }
}
