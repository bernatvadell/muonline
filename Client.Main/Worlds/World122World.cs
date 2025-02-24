using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World122World : WalkableWorldControl
    {
        public World122World() : base(worldIndex: 122) // 4TH QUEST
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(147, 29);
            base.AfterLoad();
        }
    }
}
