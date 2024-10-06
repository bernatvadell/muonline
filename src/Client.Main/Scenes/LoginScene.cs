using Client.Data.BMD;
using Client.Data.OBJS;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
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
    public class LoginScene : GameControl
    {
        public LoginScene()
        {
            Controls.Add(new LoginWorld());
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);

            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");
        }
    }
}
