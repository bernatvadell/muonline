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
        private TextureControl _logo;

        public MuLogo()
        {
            //Controls.Add(new TextureControl
            //{
            //    TexturePath = "Logo/MU-logo_g.jpg",
            //    BlendState = BlendState.Additive,
            //    Scale = 0.5f
            //});

            Controls.Add(_logo = new TextureControl
            {
                TexturePath = "Logo/MU-logo.tga",
                BlendState = Blendings.Alpha,
                Scale = 1f
            });
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Width = _logo.ScreenLocation.Width;
            Height = _logo.ScreenLocation.Height;
        }
    }
}
