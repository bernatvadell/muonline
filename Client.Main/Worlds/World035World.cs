using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    internal class World035World : WalkableWorldControl
    {
        public World035World() : base(worldIndex: 35) // CRYWOLF
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(121, 25);
            base.AfterLoad();
        }
    }
}
