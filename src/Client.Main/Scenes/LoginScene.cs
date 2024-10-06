using Client.Data.BMD;
using Client.Data.OBJS;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI;
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
    public class LoginScene : BaseScene
    {
        private LoginWorld _world;
        private MuLogo _logo;

        public LoginScene()
        {
            Controls.Add(_world = new LoginWorld());
            Controls.Add(_logo = new MuLogo() { Y = 10 });
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _logo.X = ScreenWidth / 2 - _logo.ScreenWidth / 2;
        }
    }
}
