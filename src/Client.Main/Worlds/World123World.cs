using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World123World : WalkableWorldControl
    {
        public World123World() : base(worldIndex: 123) // SWAMP OF DARKNESS
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(135, 117);
            base.AfterLoad();
        }
    }
}
