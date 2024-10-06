using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class MessageWindow : TextureControl
    {
        private readonly LabelControl _label;
        private readonly OkButton _button;

        public string Text { get => _label.Text; set => _label.Text = value; }

        private MessageWindow()
        {
            BlendState = BlendState.AlphaBlend;
            TexturePath = "Interface/message_back.tga";
            OffsetWidth = 160;
            OffsetHeight = 15;
            Controls.Add(_label = new LabelControl { Align = Align.Center, X = 10, Y = 40 });
            Controls.Add(_button = new OkButton());
        }
       
        public override void AfterLoad()
        {
            base.AfterLoad();

            X = (MuGame.Instance.Width / 2) - (ScreenWidth / 2);
            Y = (MuGame.Instance.Height / 2) - (ScreenHeight / 2);
            _label.Width = ScreenWidth;
            _button.X = (ScreenWidth / 2) - (_button.ScreenWidth / 2);
            _button.Y = ScreenHeight - _button.ScreenHeight - 15;
        }

        public static void Show(string message, Action onClose)
        {
            Task.Run(async () =>
            {
                var control = new MessageWindow
                {
                    Text = message
                };

                control._button.Click += (sender, e) =>
                {
                    onClose?.Invoke();
                    MuGame.Instance.ActiveScene.Controls.Remove(control);
                };

                MuGame.Instance.ActiveScene.Controls.Add(control);

                await control.Initialize(MuGame.Instance.GraphicsDevice);
            });
        }
    }
}
