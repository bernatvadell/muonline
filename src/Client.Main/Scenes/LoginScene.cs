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

            var nonEventGroup = new ServerGroupSelector(false)
            {
                Y = 160,
                X = 150
            };

            for (byte i = 0; i < 5; i++)
                nonEventGroup.AddServer(i, $"Server {i + 1}");

            var eventGroup = new ServerGroupSelector(true)
            {
                Y = 160,
                X = 520
            };

            for (byte i = 0; i < 5; i++)
                eventGroup.AddServer(i, $"Event {i + 1}");

            nonEventGroup.SelectedIndexChanged += (sender, e) =>
            {
                eventGroup.UnselectServer();
            };

            eventGroup.SelectedIndexChanged += (sender, e) =>
            {
                nonEventGroup.UnselectServer();
            };

            Controls.Add(nonEventGroup);
            Controls.Add(eventGroup);
        }

        public override async Task Load()
        {
            await base.Load();
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _logo.X = ScreenWidth / 2 - _logo.ScreenWidth / 2;
        }
    }
}
