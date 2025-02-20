using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World072World : WalkableWorldControl
    {
        public World072World() : base(worldIndex: 72) // IMPERIAL GUARDIAN (GAION)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(154, 186);
            base.AfterLoad();
        }
    }
}
