using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World010World : WalkableWorldControl
    {
        public World010World() : base(worldIndex: 10) // DEVIL SQUARE
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(200, 58);
            base.AfterLoad();
        }
    }
}
