using Client.Main.Core.Models;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Login
{
    public class ServerSelectEventArgs : EventArgs
    {
        public byte Index { get; set; }
        public string Name { get; set; }
    }

    public class ServerList : UIControl
    {
        private readonly List<ServerButton> _serverButtons = new();

        public event EventHandler<ServerSelectEventArgs> ServerClick;

        public void AddServer(byte index, string name, byte gauge)
        {
            var button = new ServerButton
            {
                Index = index,
                Name = name,
                X = 0,
                Y = index * 26,
                Gauge = gauge
            };
            button.Click += (s, e) => ServerClick?.Invoke(this, new ServerSelectEventArgs { Index = index, Name = name });
            _serverButtons.Add(button);
            Controls.Add(button);
        }

        public void Clear()
        {
            _serverButtons.Clear();
            Controls.Clear();
        }

        public void SetServers(IReadOnlyList<ServerInfo> servers)
        {
            Clear();
            if (servers == null)
            {
                return;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                byte gauge = server.LoadPercentage;
                AddServer((byte)server.ServerId, server.ServerName ?? $"Server {server.ServerId}", gauge);
            }
        }
    }
}
