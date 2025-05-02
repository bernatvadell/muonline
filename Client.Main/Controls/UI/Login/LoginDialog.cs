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
        private readonly OkButton _okButton; // Dodaj pole dla przycisku

        public string ServerName { get => _serverNameLabel.Text; set => _serverNameLabel.Text = value; }

        // **** NOWE WŁAŚCIWOŚCI ****
        /// <summary>
        /// Pobiera nazwę użytkownika wpisaną w polu tekstowym.
        /// </summary>
        public string Username => _userInput.Value;

        /// <summary>
        /// Pobiera hasło wpisane w polu tekstowym.
        /// </summary>
        public string Password => _passwordInput.Value;
        // **** KONIEC NOWYCH WŁAŚCIWOŚCI ****

        // **** NOWY EVENT ****
        /// <summary>
        /// Wywoływany, gdy użytkownik potwierdzi logowanie (kliknie OK lub naciśnie Enter w haśle).
        /// </summary>
        public event EventHandler? LoginAttempt;
        // **** KONIEC NOWEGO EVENTU ****

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
            _passwordInput = new TextFieldControl { X = 100, Y = 117, MaskValue = true, Skin = TextFieldSkin.NineSlice };
            // ZMIEŃ OBSŁUGĘ EVENTU HASŁA
            _passwordInput.ValueChanged += PasswordInput_EnterPressed; // Użyj dedykowanej metody
            Controls.Add(_userInput);
            Controls.Add(_passwordInput);

            _userInput.Click += (s, e) => { _userInput.OnFocus(); _passwordInput.OnBlur(); };
            _passwordInput.Click += (s, e) => { _passwordInput.OnFocus(); _userInput.OnBlur(); };

            // ZMIEŃ OBSŁUGĘ EVENTU PRZYCISKU
            _okButton = new OkButton { Y = 160, Align = ControlAlign.HorizontalCenter };
            _okButton.Click += OkButton_Click; // Użyj dedykowanej metody
            Controls.Add(_okButton);

            // Ustaw fokus na polu użytkownika na starcie
            // Potrzebujemy opóźnienia, aby kontrolka była gotowa
            // Lepiej zrobić to w scenie po pokazaniu dialogu
            // _userInput.OnFocus();
        }

        // Metoda wywoływana po kliknięciu przycisku OK
        private void OkButton_Click(object? sender, EventArgs e)
        {
            AttemptLogin();
        }

        // Metoda wywoływana po naciśnięciu Enter w polu hasła
        private void PasswordInput_EnterPressed(object? sender, EventArgs e)
        {
            // ValueChanged jest też wywoływane przy zmianie tekstu,
            // więc sprawdzamy, czy Enter został właśnie naciśnięty.
            // Lepszym rozwiązaniem byłby dedykowany event KeyDown/EnterPressed w TextFieldControl.
            // Na razie zakładamy, że ValueChanged w haśle oznacza Enter.
            // Możesz dodać sprawdzanie Keys.Enter w Update, jeśli ValueChanged jest zbyt ogólne.
            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Enter) && MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Enter))
            {
                AttemptLogin();
            }
        }

        // Wywołuje event LoginAttempt
        private void AttemptLogin()
        {
            Console.WriteLine("LoginDialog: Login attempt triggered."); // Debug log
            LoginAttempt?.Invoke(this, EventArgs.Empty);
        }

        // Ustawia fokus na polu użytkownika (wywoływane ze sceny)
        public void FocusUsername()
        {
            MuGame.ScheduleOnMainThread(() => // Upewnij się, że jest w głównym wątku
            {
                _userInput?.OnFocus();
                _passwordInput?.OnBlur();
            });
        }

        protected override void OnScreenSizeChanged()
        {
            _line1.ViewSize = new Point(DisplaySize.X - 20, 8);
            _line2.ViewSize = new Point(DisplaySize.X - 20, 5);
            base.OnScreenSizeChanged();
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
    }
}
