using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects;
using Client.Main.Worlds;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        public WalkableWorldControl World { get; private set; }

        public GameScene()
        {
            Controls.Add(World = new LorenciaWorld());
        }
    }
}
