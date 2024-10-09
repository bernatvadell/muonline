using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class MessageWindow : DialogControl
    {
        private readonly TextureControl _container;
        private readonly LabelControl _label;
        private readonly OkButton _button;

        public string Text { get => _label.Text; set => _label.Text = value; }

        private MessageWindow()
        {
            Align = ControlAlign.HorizontalCenter & ControlAlign.VerticalCenter;

            Controls.Add(_container = new TextureControl
            {
                BlendState = Blendings.Alpha,
                TexturePath = "Interface/message_back.tga",
                OffsetWidth = 160,
                OffsetHeight = 15,
            });
            Controls.Add(_label = new LabelControl { Align = ControlAlign.HorizontalCenter, Y = 40 });
            Controls.Add(_button = new OkButton() { Align = ControlAlign.Bottom | ControlAlign.HorizontalCenter, Margin = new Margin() { Bottom = 15 } });
            _button.Click += (s, e) => Close();
        }

        public static MessageWindow Show(string text)
        {
            var window = new MessageWindow { Text = text };
            MuGame.Instance.ActiveScene.Controls.Add(window);
            window.ShowDialog();
            return window;
        }
    }
}
