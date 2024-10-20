using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World131World : WalkableWorldControl
    {
        public World131World() : base(worldIndex: 131) // ABYSS OF ATLANS 03
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(177, 157);
            base.AfterLoad();
        }
    }
}
