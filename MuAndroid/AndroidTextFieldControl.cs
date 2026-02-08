using Client.Main.Controls.UI;
using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MuAndroid
{
    public class AndroidTextFieldControl : TextFieldControl
    {
        public override void OnFocus()
        {
            base.OnFocus();

            Task.Run(async () =>
            {
                var result = await KeyboardInput.Show(
                    title: Label,
                    description: Placeholder,
                    defaultText: Value,
                    usePasswordMode: MaskValue
                );

                if (result != null)
                    Value = result;
            }).ConfigureAwait(false);


            //// Subscribe to Android text input event (Critical for soft keyboard and scrcpy)
            //AndroidKeyboard.TextInput += OnTextInput;
            //AndroidKeyboard.Show();
        }

        public override void OnBlur()
        {
            base.OnBlur();

            //AndroidKeyboard.TextInput -= OnTextInput;
            //AndroidKeyboard.Hide();
        }

        private void OnTextInput(object sender, TextInputEventArgs e)
        {
            bool textChanged = false;

            // Handle control keys by character or key code
            if (e.Character == '\r' || e.Key == Keys.Enter)
            {
                OnEnterKeyPressed();
                OnValueChanged();
                return; // Enter usually consumes the event
            }
            else if (e.Character == '\b' || e.Key == Keys.Back)
            {
                // Backspace - delete last character
                if (_inputText.Length > 0)
                {
                    _inputText.Remove(_inputText.Length - 1, 1);
                    textChanged = true;
                }
            }
            else if (e.Character != '\0' && !char.IsControl(e.Character))
            {
                // Standard printable character input
                _inputText.Append(e.Character);
                textChanged = true;
            }

            if (textChanged)
            {
                UpdateScrollOffset();
                MoveCursorToEnd();
                OnValueChanged();
            }
        }
    }
}
