using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace Client.Main.Controls.UI.Login
{
    public class LoginDialog : PopupFieldDialog
    {
        private readonly TextureControl _line1;
        private readonly TextureControl _line2;
        private readonly TextFieldControl _userInput;
        private readonly LabelControl _serverNameLabel;
        private readonly TextFieldControl _passwordInput;

        public string ServerName { get => _serverNameLabel.Text; set => _serverNameLabel.Text = value; }

        public LoginDialog()
        {
            ControlSize = new Point(300, 200);
            Controls.Add(new LabelControl() { Text = "MU Online", Align = ControlAlign.HorizontalCenter, Y = 15, FontSize = 12 });
            Controls.Add(_line1 = new TextureControl { TexturePath = "Interface/GFx/popup_line_m.ozd", X = 10, Y = 40, AutoViewSize = false });
            Controls.Add(_serverNameLabel = new LabelControl() { Text = "OpenMU Server 1", Align = ControlAlign.HorizontalCenter, Y = 55, FontSize = 12, TextColor = new Color(241, 188, 37) });
            Controls.Add(new LabelControl { Text = "User", Y = 90, X = 20, AutoViewSize = false, ViewSize = new Point(70, 20), TextAlign = HorizontalAlign.Right, FontSize = 12f });
            Controls.Add(new LabelControl { Text = "Password", Y = 120, X = 20, AutoViewSize = false, ViewSize = new Point(70, 20), TextAlign = HorizontalAlign.Right, FontSize = 12f });
            Controls.Add(_line2 = new TextureControl { TexturePath = "Interface/GFx/popup_line_m.ozd", X = 10, Y = 150, AutoViewSize = false, Alpha = 0.7f });

            _userInput = new TextFieldControl { X = 100, Y = 87, Skin = TextFieldSkin.NineSlice };
            _passwordInput = new TextFieldControl { X = 100, Y = 117, MaskValue =  true, Skin = TextFieldSkin.NineSlice };
            _passwordInput.ValueChanged += OnLoginClick;
            Controls.Add(_userInput);
            Controls.Add(_passwordInput);

            // Add mouse click event handlers to change focus when clicking on the text fields
            _userInput.Click += (s, e) =>
            {
                // Set focus to user input and remove focus from password input
                _userInput.OnFocus();
                _passwordInput.OnBlur();
            };

            _passwordInput.Click += (s, e) =>
            {
                // Set focus to password input and remove focus from user input
                _passwordInput.OnFocus();
                _userInput.OnBlur();
            };

            var button = new OkButton { Y = 160, Align = ControlAlign.HorizontalCenter };
            button.Click += OnLoginClick;
            Controls.Add(button);

            _userInput.OnFocus();
        }

        private void OnLoginClick(object sender, EventArgs e)
        {
            // Change scene after login
            MuGame.Instance.ChangeScene<SelectCharacterScene>();
        }

        protected override void OnScreenSizeChanged()
        {
            _line1.ViewSize = new Point(DisplaySize.X - 20, 8);
            _line2.ViewSize = new Point(DisplaySize.X - 20, 5);
            base.OnScreenSizeChanged();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Handle Tab key to switch focus between input fields
            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Tab) && MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Tab))
            {
                if (_userInput.IsFocused)
                {
                    _userInput.OnBlur();
                    _passwordInput.OnFocus();
                }
                else if (_passwordInput.IsFocused)
                {
                    _passwordInput.OnBlur();
                    _userInput.OnFocus();
                }
            }
        }
    }
}
