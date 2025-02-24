using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World101World : WalkableWorldControl
    {
        public World101World() : base(worldIndex: 101) // URUK MOUNTAIN (AKERON PART)
        {

        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(65, 178);
        }
    }
}
