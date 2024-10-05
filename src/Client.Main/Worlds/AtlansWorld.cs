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
    public class AtlansWorld : WalkableWorldControl
    {
        public AtlansWorld() : base(worldIndex: 8)
        {
            PositionX = 20;
            PositionY = 20;
        }
    }
}
