using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World092World : WalkableWorldControl
    {
        public World092World() : base(worldIndex: 92) // AKERON (ALKMAR, UBAID & ARKA WAR)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(53, 193);
            base.AfterLoad();
        }
    }
}
