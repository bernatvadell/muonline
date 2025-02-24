using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World051World : WalkableWorldControl
    {
        public World051World() : base(worldIndex: 51)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(170, 185);
            base.AfterLoad();
        }
    }
}
