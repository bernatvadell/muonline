using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World070World : WalkableWorldControl
    {
        public World070World() : base(worldIndex: 70) // IMPERIAL GUARDIAN (GAION)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(231, 15);
            base.AfterLoad();
        }
    }
}
