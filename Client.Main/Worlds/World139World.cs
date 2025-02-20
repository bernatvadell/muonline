using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World139World : WalkableWorldControl
    {
        public World139World() : base(worldIndex: 139) // IGNIS VOLCANO
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(50, 160);
            base.AfterLoad();
        }
    }
}
