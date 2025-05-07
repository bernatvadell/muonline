using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace Client.Main.Controls.UI.Login
{
    public class LoginDialog : PopupFieldDialog
    {
        // Fields
        private readonly TextureControl _line1;
        private readonly TextureControl _line2;
        private readonly TextFieldControl _userInput;
        private readonly LabelControl _serverNameLabel;
        private readonly TextFieldControl _passwordInput;
        private readonly OkButton _okButton;

        // Properties
        public string ServerName
        {
            get => _serverNameLabel.Text;
            set => _serverNameLabel.Text = value;
        }

        /// <summary>
        /// Gets the username entered in the text field.
        /// </summary>
        public string Username => _userInput.Value;

        /// <summary>
        /// Gets the password entered in the text field.
        /// </summary>
        public string Password => _passwordInput.Value;

        // Events
        /// <summary>
        /// Invoked when the user confirms login (clicks OK or presses Enter in the password field).
        /// </summary>
        public event EventHandler LoginAttempt;

        // Constructors
        public LoginDialog()
        {
            ControlSize = new Point(300, 200);

            Controls.Add(new LabelControl
            {
                Text = "MU Online",
                Align = ControlAlign.HorizontalCenter,
                Y = 15,
                FontSize = 12
            });

            Controls.Add(_line1 = new TextureControl
            {
                TexturePath = "Interface/GFx/popup_line_m.ozd",
                X = 10,
                Y = 40,
                AutoViewSize = false
            });

            Controls.Add(_serverNameLabel = new LabelControl
            {
                Text = "OpenMU Server 1",
                Align = ControlAlign.HorizontalCenter,
                Y = 55,
                FontSize = 12,
                TextColor = new Color(241, 188, 37)
            });

            Controls.Add(new LabelControl
            {
                Text = "User",
                Y = 90,
                X = 20,
                AutoViewSize = false,
                ViewSize = new Point(70, 20),
                TextAlign = HorizontalAlign.Right,
                FontSize = 12f
            });

            Controls.Add(new LabelControl
            {
                Text = "Password",
                Y = 120,
                X = 20,
                AutoViewSize = false,
                ViewSize = new Point(70, 20),
                TextAlign = HorizontalAlign.Right,
                FontSize = 12f
            });

            Controls.Add(_line2 = new TextureControl
            {
                TexturePath = "Interface/GFx/popup_line_m.ozd",
                X = 10,
                Y = 150,
                AutoViewSize = false,
                Alpha = 0.7f
            });

            _userInput = new TextFieldControl
            {
                X = 100,
                Y = 87,
                Skin = TextFieldSkin.NineSlice
            };

            _passwordInput = new TextFieldControl
            {
                X = 100,
                Y = 117,
                MaskValue = true,
                Skin = TextFieldSkin.NineSlice
            };
            _passwordInput.ValueChanged += PasswordInput_EnterPressed; // Use dedicated method
            Controls.Add(_userInput);
            Controls.Add(_passwordInput);

            _userInput.Click += (s, e) => { _userInput.OnFocus(); _passwordInput.OnBlur(); };
            _passwordInput.Click += (s, e) => { _passwordInput.OnFocus(); _userInput.OnBlur(); };

            _okButton = new OkButton
            {
                Y = 160,
                Align = ControlAlign.HorizontalCenter
            };
            _okButton.Click += OkButton_Click; // Use dedicated method
            Controls.Add(_okButton);
        }

        // Public Methods
        /// <summary>
        /// Sets focus on the username field (called from the scene).
        /// </summary>
        public void FocusUsername()
        {
            MuGame.ScheduleOnMainThread(() => // Ensure it's on the main thread
            {
                _userInput?.OnFocus();
                _passwordInput?.OnBlur();
            });
        }

        public override void Update(GameTime gameTime)
        {
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
            base.Update(gameTime);
        }

        // Protected Methods
        protected override void OnScreenSizeChanged()
        {
            _line1.ViewSize = new Point(DisplaySize.X - 20, 8);
            _line2.ViewSize = new Point(DisplaySize.X - 20, 5);
            base.OnScreenSizeChanged();
        }

        // Private Methods
        // Method called after clicking the OK button
        private void OkButton_Click(object sender, EventArgs e)
        {
            AttemptLogin();
        }

        // Method called after pressing Enter in the password field
        private void PasswordInput_EnterPressed(object sender, EventArgs e)
        {
            // ValueChanged is also invoked on text change,
            // so we check if Enter was just pressed.
            bool enterPressed = MuGame.Instance.Keyboard.IsKeyDown(Keys.Enter) &&
                                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Enter);

            if (enterPressed)
            {
                AttemptLogin();
            }
        }

        // Invokes the LoginAttempt event
        private void AttemptLogin()
        {
            Console.WriteLine("LoginDialog: Login attempt triggered."); // Debug log
            LoginAttempt?.Invoke(this, EventArgs.Empty);
        }
    }
}