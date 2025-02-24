using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World083World : WalkableWorldControl
    {
        public World083World() : base(worldIndex: 83) // DOPPELGANGER RENEWAL
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(122, 127);
            base.AfterLoad();
        }
    }
}
