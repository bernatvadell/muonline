using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World094World : WalkableWorldControl
    {
        public World094World() : base(worldIndex: 94)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(125, 193);
            base.AfterLoad();
        }
    }
}
