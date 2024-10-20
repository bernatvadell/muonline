using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World141World : WalkableWorldControl
    {
        public World141World() : base(worldIndex: 141) // BLOODY TARKAN
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(240, 180);
            base.AfterLoad();
        }
    }
}
