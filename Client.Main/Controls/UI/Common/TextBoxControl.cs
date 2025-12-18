using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Text;

namespace Client.Main.Controls.UI.Common
{
    /// <summary>
    /// A simple text input control for entering text.
    /// </summary>
    public class TextBoxControl : GameControl
    {
        private StringBuilder _text = new StringBuilder();
        private bool _isFocused = false;
        private double _cursorBlinkTimer = 0;
        private bool _cursorVisible = true;
        private KeyboardState _previousKeyboardState;
        
        public string Text
        {
            get => _text.ToString();
            set
            {
                _text.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    _text.Append(value.Substring(0, Math.Min(value.Length, MaxLength)));
                }
            }
        }

        public int MaxLength { get; set; } = 50;
        public string PlaceholderText { get; set; } = "";
        public float FontSize { get; set; } = 14f;
        public Color TextColor { get; set; } = Color.White;
        public Color PlaceholderColor { get; set; } = Color.Gray;
        public new Color BackgroundColor { get; set; } = new Color(32, 32, 42, 200);
        public new Color BorderColor { get; set; } = Color.Gray;
        public Color FocusedBorderColor { get; set; } = Color.Gold;
        public new int BorderThickness { get; set; } = 1;
        public new int Padding { get; set; } = 8;

        public TextBoxControl()
        {
            Interactive = true;
            AutoViewSize = false;
            ViewSize = new Point(200, 30);
            _previousKeyboardState = Keyboard.GetState();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_isFocused)
            {
                // Update cursor blink
                _cursorBlinkTimer += gameTime.ElapsedGameTime.TotalSeconds;
                if (_cursorBlinkTimer >= 0.5)
                {
                    _cursorVisible = !_cursorVisible;
                    _cursorBlinkTimer = 0;
                }

                // Handle keyboard input
                var currentKeyboardState = Keyboard.GetState();
                var pressedKeys = currentKeyboardState.GetPressedKeys();

                foreach (var key in pressedKeys)
                {
                    if (_previousKeyboardState.IsKeyUp(key))
                    {
                        HandleKeyPress(key, currentKeyboardState);
                    }
                }

                _previousKeyboardState = currentKeyboardState;
            }
        }

        private void HandleKeyPress(Keys key, KeyboardState keyboardState)
        {
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

            if (key == Keys.Back && _text.Length > 0)
            {
                _text.Remove(_text.Length - 1, 1);
                return;
            }

            if (key == Keys.Delete)
            {
                _text.Clear();
                return;
            }

            if (key == Keys.Enter || key == Keys.Escape)
            {
                _isFocused = false;
                return;
            }

            if (_text.Length >= MaxLength)
            {
                return;
            }

            // Convert key to character
            char? c = KeyToChar(key, shift);
            if (c.HasValue)
            {
                _text.Append(c.Value);
            }
        }

        private char? KeyToChar(Keys key, bool shift)
        {
            // Letters
            if (key >= Keys.A && key <= Keys.Z)
            {
                char baseChar = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(baseChar) : baseChar;
            }

            // Numbers
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shift)
                {
                    return key switch
                    {
                        Keys.D1 => '!',
                        Keys.D2 => '@',
                        Keys.D3 => '#',
                        Keys.D4 => '$',
                        Keys.D5 => '%',
                        Keys.D6 => '^',
                        Keys.D7 => '&',
                        Keys.D8 => '*',
                        Keys.D9 => '(',
                        Keys.D0 => ')',
                        _ => null
                    };
                }
                return (char)('0' + (key - Keys.D0));
            }

            // NumPad numbers
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            // Special characters
            return key switch
            {
                Keys.Space => ' ',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemComma => shift ? '<' : ',',
                _ => null
            };
        }

        public override bool OnClick()
        {
            _isFocused = true;
            _cursorVisible = true;
            _cursorBlinkTimer = 0;
            return base.OnClick();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var bounds = new Rectangle(DisplayPosition.X, DisplayPosition.Y, ViewSize.X, ViewSize.Y);

            // Background
            spriteBatch.Draw(pixel, bounds, BackgroundColor);

            // Border
            var borderColor = _isFocused ? FocusedBorderColor : BorderColor;
            DrawBorder(spriteBatch, bounds, borderColor, BorderThickness);

            // Text
            var font = GraphicsManager.Instance.Font;
            if (font != null)
            {
                string displayText = _text.Length > 0 ? _text.ToString() : PlaceholderText;
                var textColor = _text.Length > 0 ? TextColor : PlaceholderColor;
                var textPosition = new Vector2(
                    DisplayPosition.X + Padding,
                    DisplayPosition.Y + (ViewSize.Y - font.LineSpacing) / 2
                );

                spriteBatch.DrawString(font, displayText, textPosition, textColor);

                // Cursor
                if (_isFocused && _cursorVisible && _text.Length > 0)
                {
                    var textSize = font.MeasureString(_text.ToString());
                    var cursorX = textPosition.X + textSize.X;
                    var cursorRect = new Rectangle(
                        (int)cursorX,
                        (int)textPosition.Y,
                        2,
                        font.LineSpacing
                    );
                    spriteBatch.Draw(pixel, cursorRect, TextColor);
                }
            }

            base.Draw(gameTime);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        public new void Focus()
        {
            _isFocused = true;
            _cursorVisible = true;
            _cursorBlinkTimer = 0;
        }

        public void Unfocus()
        {
            _isFocused = false;
        }
    }
}
