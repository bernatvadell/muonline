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
    public class DeviasWorld : WalkableWorldControl
    {
        public DeviasWorld() : base(worldIndex: 3)
        {
            PositionX = 219;
            PositionY = 24;
        }
    }
}
