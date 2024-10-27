using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Login;
using Client.Main.Models;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using Pipelines.Sockets.Unofficial;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoginScene : BaseScene
    {

        public LoginScene()
        {
            // Controls.Add(new MuLogo() { Y = 10, Align = ControlAlign.HorizontalCenter });
        }

        public override async Task Load()
        {
            await ChangeWorldAsync<NewLoginWorld>();
            await base.Load();
            SoundController.Instance.PlayBackgroundMusic("Music/login_theme.mp3");
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            // TryConnect();
            OnConnect();
        }

        private void TryConnect()
        {
            try
            {
                var tcpClient = new TcpClient(Constants.IPAddress, Constants.Port);
                var socketConnection = SocketConnection.Create(tcpClient.Client);
                var connection = new Connection(socketConnection, null, null, new NullLogger<Connection>());

                connection.Disconnected += OnDiscconected;
                connection.PacketReceived += PacketReceived;

                OnConnect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                OnDiscconected().ConfigureAwait(false);
            }
        }

        private void OnConnect()
        {
            var nonEventGroup = new ServerGroupSelector(false)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Left = -220 }
            };

            for (byte i = 0; i < 4; i++)
                nonEventGroup.AddServer(i, $"Server {i + 1}");

            var eventGroup = new ServerGroupSelector(true)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Right = -220 }
            };

            for (byte i = 0; i < 3; i++)
                eventGroup.AddServer(i, $"Event {i + 1}");

            var serverList = new ServerList();
            serverList.Visible = false;
            serverList.ServerClick += ServerList_ServerClick;

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

        private void ServerList_ServerClick(object sender, ServerSelectEventArgs e)
        {
            MuGame.Instance.ChangeScene<SelectCharacterScene>();
        }

        private ValueTask PacketReceived(System.Buffers.ReadOnlySequence<byte> eventArgs)
        {
            return ValueTask.CompletedTask;
        }

        private ValueTask OnDiscconected()
        {
            var dialog = MessageWindow.Show("You are disconnected from server");
            dialog.Closed += (s, e) => MuGame.Instance.Exit();
            return ValueTask.CompletedTask;
        }
    }
}
