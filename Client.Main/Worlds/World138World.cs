using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World138World : WalkableWorldControl
    {
        public World138World() : base(worldIndex: 138) // KANTURU UNDERGROUNDS
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(130, 130);
            base.AfterLoad();
        }
    }
}
