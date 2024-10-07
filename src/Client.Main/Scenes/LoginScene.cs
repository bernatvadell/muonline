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
        private readonly LoginWorld _world;
        private readonly MuLogo _logo;

        public LoginScene()
        {
            Controls.Add(_world = new LoginWorld());
            Controls.Add(_logo = new MuLogo() { Y = 10, Align = ControlAlign.HorizontalCenter });

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

            var serverList = new ServerList();
            serverList.Visible = false;

            nonEventGroup.SelectedIndexChanged += (sender, e) =>
            {
                serverList.Clear();

                for (var i = 0; i < 10; i++)
                    serverList.AddServer((byte)i, $"Non Event Server {nonEventGroup.ActiveIndex + 1}", (byte)((i + 1) * 10));

                serverList.X = MuGame.Instance.Width / 2 - serverList.Width / 2;
                serverList.Y = MuGame.Instance.Height / 2 - serverList.Height / 2;
                serverList.Visible = true;

                eventGroup.UnselectServer();
            };

            eventGroup.SelectedIndexChanged += (sender, e) =>
            {
                serverList.Clear();

                for (var i = 0; i < 10; i++)
                    serverList.AddServer((byte)i, $"Event Server {eventGroup.ActiveIndex + 1}", (byte)((i + 1) * 10));

                serverList.X = MuGame.Instance.Width / 2 - serverList.Width / 2;
                serverList.Y = MuGame.Instance.Height / 2 - serverList.Height / 2;
                serverList.Visible = true;

                nonEventGroup.UnselectServer();
            };

            Controls.Add(nonEventGroup);
            Controls.Add(eventGroup);
            Controls.Add(serverList);
        }

        public override async Task Load()
        {
            await base.Load();
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");
        }
    }
}
