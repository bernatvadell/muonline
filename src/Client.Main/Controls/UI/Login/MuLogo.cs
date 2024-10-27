using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Login
{
    public class MuLogo : UIControl
    {
        public MuLogo()
        {
            Controls.Add(new TextureControl
            {
                TexturePath = "Logo/MU-logo.tga",
                BlendState = Blendings.Alpha,
                Scale = 1f
            });
        }

    }
}
