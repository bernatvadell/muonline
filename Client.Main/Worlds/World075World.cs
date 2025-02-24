using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World075World : WalkableWorldControl
    {
        public World075World() : base(worldIndex: 75)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(93, 67);
            base.AfterLoad();
        }
    }
}
