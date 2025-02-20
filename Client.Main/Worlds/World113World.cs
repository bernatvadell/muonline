using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World113World : WalkableWorldControl
    {
        public World113World() : base(worldIndex: 113) // FEREA
        {

        }

        public override void AfterLoad()
        {       
            Walker.Location = new Vector2(236, 154);
            base.AfterLoad();
        }
    }
}
