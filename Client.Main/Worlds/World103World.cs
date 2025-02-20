using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World103World : WalkableWorldControl
    {
        public World103World() : base(worldIndex: 103) // TORMENTED SQUARE
        {

        }

        public override void AfterLoad()
        {       
            Walker.Location = new Vector2(99, 145);
            base.AfterLoad();
        }
    }
}
