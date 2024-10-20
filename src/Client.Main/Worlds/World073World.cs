using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World073World : WalkableWorldControl
    {
        public World073World() : base(worldIndex: 73) // IMPERIAL GUARDIAN (GAION)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(93, 67);
            base.AfterLoad();
        }
    }
}
