using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World064World : WalkableWorldControl
    {
        public World064World() : base(worldIndex: 64) // VULCANUS
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(170, 185);
            base.AfterLoad();
        }
    }
}
