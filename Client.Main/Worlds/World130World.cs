using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World130World : WalkableWorldControl
    {
        public World130World() : base(worldIndex: 130) // ABYSS OF ATLANS 02
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(42, 70);
            base.AfterLoad();
        }
    }
}
