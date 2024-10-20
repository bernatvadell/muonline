using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World135World : WalkableWorldControl
    {
        public World135World() : base(worldIndex: 135) // ASHEN AIDA (GRAY AIDA)
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(232, 88);
            base.AfterLoad();
        }
    }
}
