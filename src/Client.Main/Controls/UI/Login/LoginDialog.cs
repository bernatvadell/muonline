using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Login
{
    public class LoginDialog : PopupFieldDialog
    {
        private readonly TextureControl _line1;
        private readonly TextureControl _line2;
        private LabelControl _serverNameLabel;

        public string ServerName { get => _serverNameLabel.Text; set => _serverNameLabel.Text = value; }

        public LoginDialog()
        {
            ControlSize = new Point(300, 200);
            Controls.Add(new LabelControl() { Text = "MU Online", Align = ControlAlign.HorizontalCenter, Y = 15, FontSize = 12 });
            Controls.Add(_line1 = new TextureControl { TexturePath = "Interface/GFx/popup_line_m.ozd", X = 10, Y = 40, AutoViewSize = false });
            Controls.Add(_serverNameLabel = new LabelControl() { Text = "OpenMU Server 1", Align = ControlAlign.HorizontalCenter, Y = 55, FontSize = 12, TextColor = new Color(241, 188, 37) });
            Controls.Add(new LabelControl { Text = "User", Y = 90, X = 20, AutoViewSize = false, ViewSize = new(70, 20), TextAlign = HorizontalAlign.Right });
            Controls.Add(new LabelControl { Text = "Password", Y = 120, X = 20, AutoViewSize = false, ViewSize = new(70, 20), TextAlign = HorizontalAlign.Right });
            Controls.Add(_line2 = new TextureControl { TexturePath = "Interface/GFx/popup_line_m.ozd", X = 10, Y = 150, AutoViewSize = false, Alpha = 0.7f });

            var button = new OkButton { Y = 160, Align = ControlAlign.HorizontalCenter };
            button.Click += OnLoginClick;
            Controls.Add(button);
        }

        private void OnLoginClick(object sender, EventArgs e)
        {
            MuGame.Instance.ChangeScene<SelectCharacterScene>();
        }

        protected override void OnScreenSizeChanged()
        {
            _line1.ViewSize = new(DisplaySize.X - 20, 8);
            _line2.ViewSize = new(DisplaySize.X - 20, 5);
            base.OnScreenSizeChanged();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
