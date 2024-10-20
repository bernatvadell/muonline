using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World032World : WalkableWorldControl
    {
        public World032World() : base(worldIndex: 32) // LAND OF TRIALS
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(60, 20);
            base.AfterLoad();
        }
    }
}
