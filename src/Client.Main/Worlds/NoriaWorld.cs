using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects;
using Client.Main.Objects.Noria;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class NoriaWorld : WalkableWorldControl
    {
        public NoriaWorld() : base(worldIndex: 4)
        {
            
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(178, 104);
            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            MapTileObjects[39] = typeof(ChaosMachineObject);
        }
    }
}
