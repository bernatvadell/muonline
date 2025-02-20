using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World140World : WalkableWorldControl
    {
        public World140World() : base(worldIndex: 140) // BOSS BATTLE ZONE
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(131, 114);
            base.AfterLoad();
        }
    }
}
