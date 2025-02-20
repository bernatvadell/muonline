using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World143World : WalkableWorldControl
    {
        public World143World() : base(worldIndex: 143) //
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(190, 230);
            base.AfterLoad();
        }
    }
}
