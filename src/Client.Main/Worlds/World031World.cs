using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World031World : WalkableWorldControl
    {
        public World031World() : base(worldIndex: 31) // VALLEY OF LOREN
        {

        }

        public override void AfterLoad()
        { 
            Walker.Location = new Vector2(94, 230);
            base.AfterLoad();
        }
    }
}
