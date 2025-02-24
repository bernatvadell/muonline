using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World059World : WalkableWorldControl
    {
        public World059World() : base(worldIndex: 59) // RAKLION BOSS (SELUPAM)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(146, 130);
            base.AfterLoad();
        }
    }
}
