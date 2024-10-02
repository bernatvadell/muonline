using Client.Data.BMD;
using Client.Data.OBJS;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoginScene : WorldControl
    {
        public LoginScene() : base(worldIndex: 74, tourMode: true)
        {
        }
    }
}
