using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public enum TextFieldSkin
    {
        Flat,
        NineSlice
    }

    public class TextFieldControl : UIControl
    {
        private readonly StringBuilder _inputText = new();
        private double _cursorBlinkTimer;
        private bool _showCursor;
        private float _scrollOffset;

        private const int TextMargin = 5;
        private const int CursorBlinkInterval = 500;

        private Texture2D[] _nineSlice = new Texture2D[9];

        public TextFieldSkin Skin { get; set; } = TextFieldSkin.Flat;
        public Color TextColor { get; set; } = Color.White;
        public float FontSize { get; set; } = 12f;
        public TextFieldControl NextInput { get; set; }
        public bool IsFocused { get; private set; }

        public string Value
        {
            get => _inputText.ToString();
            set
            {
                _inputText.Clear();
                _inputText.Append(value ?? string.Empty);
                UpdateScrollOffset();
                MoveCursorToEnd();
            }
        }

        public bool MaskValue { get; set; }
        public event EventHandler ValueChanged;
        public event EventHandler EnterKeyPressed;

        public TextFieldControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(176, 14);
            Interactive = true;
            IsFocused = false;
        }

        public override async Task Load()
        {
            await base.Load();

            if (Skin == TextFieldSkin.NineSlice)
            {
                var names = new[] { "01", "02", "03", "04", "05", "06", "07", "08", "09" };
                for (int i = 0; i < 9; i++)
                {
                    _nineSlice[i] = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/textbg{names[i]}.ozd");
                }
            }
        }

        public override void OnFocus()
        {
            base.OnFocus();
            IsFocused = true;
            _showCursor = true;
            _cursorBlinkTimer = 0;
            if (Scene != null) Scene.FocusControl = this;
        }

        public override void OnBlur()
        {
            base.OnBlur();
            IsFocused = false;
            _showCursor = false;
            _cursorBlinkTimer = 0;
        }

        public new void Focus() => OnFocus();
        public new void Blur() => OnBlur();

        public void MoveCursorToEnd()
        {
            UpdateScrollOffset();
            if (IsFocused)
            {
                _showCursor = true;
                _cursorBlinkTimer = 0;
            }
        }

        private void UpdateScrollOffset()
        {
            if (GraphicsManager.Instance?.Font == null) return;

            float scaleFactor = FontSize / Constants.BASE_FONT_SIZE;
            var textToDisplay = MaskValue ? new string('*', _inputText.Length) : _inputText.ToString();
            var textWidth = GraphicsManager.Instance.Font.MeasureString(textToDisplay).X * scaleFactor;
            float maxVisibleWidth = DisplayRectangle.Width - TextMargin * 2;

            _scrollOffset = textWidth > maxVisibleWidth ? textWidth - maxVisibleWidth : 0;
        }

        private void ProcessKey(Keys key, bool shift, bool capsLock)
        {
            bool textChanged = false;
            if (key == Keys.Back && _inputText.Length > 0)
            {
                _inputText.Remove(_inputText.Length - 1, 1);
                textChanged = true;
            }
            else if (key == Keys.Enter)
            {
                EnterKeyPressed?.Invoke(this, EventArgs.Empty);
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                char character = KeyToChar(key, shift, capsLock);
                if (character != '\0')
                {
                    _inputText.Append(character);
                    textChanged = true;
                }
            }

            if (textChanged)
            {
                UpdateScrollOffset();
                MoveCursorToEnd();
            }
        }

        private char KeyToChar(Keys key, bool shift, bool capsLock)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                bool isUpper = capsLock ^ shift;
                char letter = (char)('A' + (key - Keys.A));
                return isUpper ? letter : char.ToLower(letter);
            }
            else if (key >= Keys.D0 && key <= Keys.D9)
            {
                char digit = (char)('0' + (key - Keys.D0));
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
                        _ => digit,
                    };
                }
                return digit;
            }
            else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }
            return key switch
            {
                Keys.Space => ' ',
                Keys.OemComma => ',',
                Keys.OemPeriod => '.',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemTilde => shift ? '~' : '`',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemSemicolon => shift ? ':' : ';',
                _ => '\0'
            };
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsFocused || !Visible) return;

            var keysPressed = MuGame.Instance.Keyboard.GetPressedKeys();
            bool shift = MuGame.Instance.Keyboard.IsKeyDown(Keys.LeftShift) || MuGame.Instance.Keyboard.IsKeyDown(Keys.RightShift);
            bool capsLock = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Console.CapsLock : false;

            bool textModifiedByKey = false;
            foreach (var key in keysPressed)
            {
                if (MuGame.Instance.PrevKeyboard.IsKeyUp(key))
                {
                    ProcessKey(key, shift, capsLock);
                    textModifiedByKey = true;
                }
            }

            if (textModifiedByKey || (IsFocused && !MuGame.Instance.PrevKeyboard.GetPressedKeys().Any()))
            {
                UpdateScrollOffset();
            }

            _cursorBlinkTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (_cursorBlinkTimer >= CursorBlinkInterval)
            {
                _showCursor = !_showCursor;
                _cursorBlinkTimer = 0;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            var spriteBatch = GraphicsManager.Instance.Sprite;

            if (Skin == TextFieldSkin.NineSlice && _nineSlice[0] != null)
                DrawNineSliceBackground(spriteBatch);
            else
                DrawFlatBackground(spriteBatch);

            DrawTextAndCursor(spriteBatch);

            base.Draw(gameTime);
        }

        private void DrawFlatBackground(SpriteBatch spriteBatch)
        {
            DrawBackground();
            DrawBorder();
        }

        private void DrawNineSliceBackground(SpriteBatch spriteBatch)
        {
            var r = DisplayRectangle;

            var TL = _nineSlice[0];
            var T = _nineSlice[1];
            var TR = _nineSlice[2];
            var L = _nineSlice[3];
            var C = _nineSlice[4];
            var R = _nineSlice[5];
            var BL = _nineSlice[6];
            var B = _nineSlice[7];
            var BR = _nineSlice[8];

            spriteBatch.Draw(TL, new Rectangle(r.X, r.Y, TL.Width, TL.Height), Color.White);
            spriteBatch.Draw(TR, new Rectangle(r.Right - TR.Width, r.Y, TR.Width, TR.Height), Color.White);
            spriteBatch.Draw(BL, new Rectangle(r.X, r.Bottom - BL.Height, BL.Width, BL.Height), Color.White);
            spriteBatch.Draw(BR, new Rectangle(r.Right - BR.Width, r.Bottom - BR.Height, BR.Width, BR.Height), Color.White);

            spriteBatch.Draw(T, new Rectangle(r.X + TL.Width, r.Y, r.Width - TL.Width - TR.Width, T.Height), Color.White);
            spriteBatch.Draw(B, new Rectangle(r.X + BL.Width, r.Bottom - B.Height, r.Width - BL.Width - BR.Width, B.Height), Color.White);
            spriteBatch.Draw(L, new Rectangle(r.X, r.Y + TL.Height, L.Width, r.Height - TL.Height - BL.Height), Color.White);
            spriteBatch.Draw(R, new Rectangle(r.Right - R.Width, r.Y + TR.Height, R.Width, r.Height - TR.Height - BR.Height), Color.White);

            spriteBatch.Draw(C, new Rectangle(r.X + L.Width, r.Y + T.Height, r.Width - L.Width - R.Width, r.Height - T.Height - B.Height), Color.White);
        }

        private void DrawTextAndCursor(SpriteBatch spriteBatch)
        {
            var font = GraphicsManager.Instance.Font;
            if (font == null) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var originalScissorRect = gd.ScissorRectangle;
            var area = new Rectangle(
                DisplayRectangle.X + TextMargin,
                DisplayRectangle.Y,
                Math.Max(0, DisplayRectangle.Width - TextMargin * 2),
                DisplayRectangle.Height
            );
            gd.ScissorRectangle = Rectangle.Intersect(originalScissorRect, area);
            gd.RasterizerState = new RasterizerState { ScissorTestEnable = true };

            float scale = FontSize / Constants.BASE_FONT_SIZE;
            string text = MaskValue ? new string('*', _inputText.Length) : _inputText.ToString();
            Vector2 textPos = new Vector2(DisplayRectangle.X + TextMargin - _scrollOffset,
                                          DisplayRectangle.Y + (DisplayRectangle.Height - font.MeasureString("A").Y * scale) / 2f);

            spriteBatch.DrawString(font, text, textPos, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (IsFocused && _showCursor)
            {
                float w = font.MeasureString(text).X * scale;
                var cursorPos = textPos + new Vector2(w, 0);
                if (cursorPos.X >= area.Left && cursorPos.X <= area.Right)
                {
                    spriteBatch.DrawString(font, "|", cursorPos, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            gd.ScissorRectangle = originalScissorRect;
            gd.RasterizerState = RasterizerState.CullNone;
        }
    }
}
