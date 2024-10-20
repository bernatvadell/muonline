using Client.Main.Controls;
using Client.Main.Objects.Arena;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class StadiumWorld : WalkableWorldControl
    {
        public StadiumWorld() : base(worldIndex: 7) // ARENA
        {

        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(56, 85);
            base.AfterLoad();
        }
    }
}
