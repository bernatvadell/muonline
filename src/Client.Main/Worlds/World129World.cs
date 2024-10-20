using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World129World : WalkableWorldControl
    {
        public World129World() : base(worldIndex: 129) // ABYSS OF ATLANS 01
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(51, 22);
            base.AfterLoad();
        }
    }
}
