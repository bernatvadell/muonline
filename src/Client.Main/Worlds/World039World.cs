using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World039World : WalkableWorldControl
    {
        public World039World() : base(worldIndex: 39) // KANTURU REMAIN (RELICS)
        {
            Terrain.TextureMappingFiles[10] = "TileRock04.OZJ";
        }

        public override void AfterLoad()
        {     
            Walker.Location = new Vector2(72, 105);
            base.AfterLoad();
        }
    }
}
