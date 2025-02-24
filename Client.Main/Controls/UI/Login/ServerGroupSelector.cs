using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Login
{
    public class ServerGroupSelector : UIControl
    {
        private List<ServerGroupButton> _buttons = [];

        public int ActiveIndex { get; set; } = -1;
        public bool IsEventList { get; set; }
        public SpriteControl IndicatorActive { get; }

        public event EventHandler SelectedIndexChanged;

        public ServerGroupSelector(bool eventList)
        {
            IsEventList = eventList;
            Visible = false;

            Controls.Add(new SpriteControl
            {
                TexturePath = "Interface/server_deco_all.tga",
                TileWidth = 67,
                TileHeight = 97,
                TileX = eventList ? 1 : 0,
                X = eventList ? 70 : 0,
                BlendState = Blendings.Alpha,
            });

            Controls.Add(IndicatorActive = new SpriteControl
            {
                X = eventList ? 0 : 114,
                Y = 15,
                TexturePath = "Interface/server_deco_all.tga",
                TileOffset = new Point(136, 0),
                TileWidth = 23,
                TileHeight = 29,
                TileY = eventList ? 1 : 0,
                Visible = false,
                BlendState = Blendings.Alpha,
            });
        }

        public void AddServer(byte index, string name)
        {
            if (!Visible) Visible = true;

            var button = new ServerGroupButton
            {
                Index = index,
                Name = name,
                X = IsEventList ? 23 : 8,
                Y = 19 + index * 27,
            };

            button.Click += ServerGroupButton_Click;

            _buttons.Add(button);

            Controls.Add(button);

            IndicatorActive.BringToFront();
        }

        public void UnselectServer()
        {
            if (ActiveIndex >= 0)
            {
                _buttons[ActiveIndex].Selected = false;
                ActiveIndex = -1;
                IndicatorActive.Visible = false;
            }
        }

        private void ServerGroupButton_Click(object sender, EventArgs e)
        {
            if (ActiveIndex >= 0)
                _buttons[ActiveIndex].Selected = false;

            var button = (ServerGroupButton)sender;
            button.Selected = true;
            ActiveIndex = button.Index;

            IndicatorActive.Visible = true;
            IndicatorActive.Y = 17 + ActiveIndex * 27;

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
