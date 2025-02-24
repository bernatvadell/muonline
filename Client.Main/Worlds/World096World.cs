using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World096World : WalkableWorldControl
    {
        public World096World() : base(worldIndex: 96) // DEBENTER (AKERON PART)
        {

        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(50, 50);
            base.AfterLoad();
        }
    }
}
