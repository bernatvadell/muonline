using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class DungeonWorld : WorldControl
    {
        public DungeonWorld() : base(worldIndex: 2)
        {
            PositionX = 232;
            PositionY = 126;
        }
    }
}
