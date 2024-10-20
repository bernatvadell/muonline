using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World099World : WalkableWorldControl
    {
        public World099World() : base(worldIndex: 99) // ILLUSION TEMPLE LEAGUE
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(135, 119);
            base.AfterLoad();
        }
    }
}
