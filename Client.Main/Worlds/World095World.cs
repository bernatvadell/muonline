using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World095World : WalkableWorldControl
    {
        public World095World() : base(worldIndex: 95)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(125, 193);
            base.AfterLoad();
        }
    }
}
