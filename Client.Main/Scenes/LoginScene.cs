using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Login;
using Client.Main.Models;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Network;
using Pipelines.Sockets.Unofficial;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoginScene : BaseScene
    {
        private LoginDialog _loginDialog;
        private ServerGroupSelector _nonEventGroup;
        private ServerGroupSelector _eventGroup;
        private ServerList _serverList;

        public LoginScene()
        {
            Controls.Add(_loginDialog = new LoginDialog()
            {
                Visible = false,
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter
            });
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            MuGame.Instance.ChangeScene<SelectCharacterScene>();
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
            _nonEventGroup = new ServerGroupSelector(false)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Left = -220 }
            };

            for (byte i = 0; i < 4; i++)
                _nonEventGroup.AddServer(i, $"Server {i + 1}");

            _eventGroup = new ServerGroupSelector(true)
            {
                Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter,
                Margin = new Margin { Right = -220 }
            };

            for (byte i = 0; i < 3; i++)
                _eventGroup.AddServer(i, $"Event {i + 1}");

            _serverList = new ServerList();
            _serverList.Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter;
            _serverList.Visible = false;
            _serverList.ServerClick += ServerList_ServerClick;

            _nonEventGroup.SelectedIndexChanged += (sender, e) =>
            {
                _serverList.Clear();

                for (var i = 0; i < 10; i++)
                    _serverList.AddServer((byte)i, $"Non Event Server {_nonEventGroup.ActiveIndex + 1}", (byte)((i + 1) * 10));

                _serverList.Visible = true;

                _eventGroup.UnselectServer();
            };

            _eventGroup.SelectedIndexChanged += (sender, e) =>
            {
                _serverList.Clear();

                for (var i = 0; i < 10; i++)
                    _serverList.AddServer((byte)i, $"Event Server {_eventGroup.ActiveIndex + 1}", (byte)((i + 1) * 10));

                _serverList.X = MuGame.Instance.Width / 2 - _serverList.DisplaySize.X / 2;
                _serverList.Y = MuGame.Instance.Height / 2 - _serverList.DisplaySize.Y / 2;
                _serverList.Visible = true;

                _nonEventGroup.UnselectServer();
            };

            Controls.Add(_nonEventGroup);
            Controls.Add(_eventGroup);
            Controls.Add(_serverList);
        }

        private void ServerList_ServerClick(object sender, ServerSelectEventArgs e)
        {
            _eventGroup.Visible = false;
            _nonEventGroup.Visible = false;
            _serverList.Visible = false;
            _loginDialog.Visible = true;
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
