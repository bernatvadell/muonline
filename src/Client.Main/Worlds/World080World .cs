using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World080World : WalkableWorldControl
    {
        public World080World() : base(worldIndex: 80) // LOREN MARKET & EVENT SQUARE (01)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(60, 50);
            base.AfterLoad();
        }
    }
}
