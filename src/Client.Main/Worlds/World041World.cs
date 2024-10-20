using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World041World : WalkableWorldControl
    {
        public World041World() : base(worldIndex: 41) // SILENT MAP (GM MAP)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(229, 25);
            base.AfterLoad();
        }
    }
}
