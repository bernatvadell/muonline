using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World063World : WalkableWorldControl
    {
        public World063World() : base(worldIndex: 63) // SANTATOWN (SANTA VILLAGE)
        {
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(220, 30);
            base.AfterLoad();
        }
    }
}
