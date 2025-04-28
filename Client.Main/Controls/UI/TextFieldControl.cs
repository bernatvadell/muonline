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
        }

        private void DrawFlatBackground(SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();
            DrawBackground();
            DrawBorder();
            spriteBatch.End();
        }

        private void DrawNineSliceBackground(SpriteBatch spriteBatch)
        {
            var TL = _nineSlice[0];
            var T = _nineSlice[1];
            var TR = _nineSlice[2];
            var L = _nineSlice[3];
            var C = _nineSlice[4];
            var R = _nineSlice[5];
            var BL = _nineSlice[6];
            var B = _nineSlice[7];
            var BR = _nineSlice[8];

            spriteBatch.Begin(SpriteSortMode.Deferred,
                              BlendState.AlphaBlend,
                              SamplerState.PointClamp);

            spriteBatch.Draw(TL, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y, TL.Width, TL.Height), Color.White);
            spriteBatch.Draw(TR, new Rectangle(DisplayRectangle.Right - TR.Width, DisplayRectangle.Y, TR.Width, TR.Height), Color.White);
            spriteBatch.Draw(BL, new Rectangle(DisplayRectangle.X, DisplayRectangle.Bottom - BL.Height, BL.Width, BL.Height), Color.White);
            spriteBatch.Draw(BR, new Rectangle(DisplayRectangle.Right - BR.Width, DisplayRectangle.Bottom - BR.Height, BR.Width, BR.Height), Color.White);

            spriteBatch.Draw(T, new Rectangle(DisplayRectangle.X + TL.Width, DisplayRectangle.Y, DisplayRectangle.Width - TL.Width - TR.Width, T.Height), Color.White);
            spriteBatch.Draw(B, new Rectangle(DisplayRectangle.X + BL.Width, DisplayRectangle.Bottom - B.Height, DisplayRectangle.Width - BL.Width - BR.Width, B.Height), Color.White);
            spriteBatch.Draw(L, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y + TL.Height, L.Width, DisplayRectangle.Height - TL.Height - BL.Height), Color.White);
            spriteBatch.Draw(R, new Rectangle(DisplayRectangle.Right - R.Width, DisplayRectangle.Y + TR.Height, R.Width, DisplayRectangle.Height - TR.Height - BR.Height), Color.White);

            spriteBatch.Draw(C, new Rectangle(DisplayRectangle.X + L.Width, DisplayRectangle.Y + T.Height, DisplayRectangle.Width - L.Width - R.Width, DisplayRectangle.Height - T.Height - B.Height), Color.White);

            spriteBatch.End();
        }

        private void DrawTextAndCursor(SpriteBatch spriteBatch)
        {
            if (GraphicsManager.Instance.Font == null) return;

            var originalScissorRect = GraphicsDevice.ScissorRectangle;
            var textRenderArea = new Rectangle(DisplayRectangle.X + TextMargin, DisplayRectangle.Y, Math.Max(0, DisplayRectangle.Width - TextMargin * 2), DisplayRectangle.Height);

            GraphicsDevice.ScissorRectangle = Rectangle.Intersect(textRenderArea, originalScissorRect);

            spriteBatch.Begin(SpriteSortMode.Deferred,
                              BlendState.AlphaBlend,
                              SamplerState.PointClamp,
                              DepthStencilState.None,
                              new RasterizerState { ScissorTestEnable = true });

            float scaleFactor = FontSize / Constants.BASE_FONT_SIZE;
            var textToDisplay = MaskValue ? new string('*', _inputText.Length) : _inputText.ToString();
            float textHeight = GraphicsManager.Instance.Font.MeasureString("A").Y * scaleFactor;
            float textOffsetY = (DisplayRectangle.Height - textHeight) / 2f;
            var textPosition = new Vector2(DisplayRectangle.X + TextMargin - _scrollOffset, DisplayRectangle.Y + textOffsetY);

            spriteBatch.DrawString(GraphicsManager.Instance.Font, textToDisplay, textPosition, TextColor, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 0f);

            if (IsFocused && _showCursor)
            {
                var textWidth = GraphicsManager.Instance.Font.MeasureString(textToDisplay).X * scaleFactor;
                var cursorPosition = textPosition + new Vector2(textWidth, 0);
                if (cursorPosition.X >= textRenderArea.Left && cursorPosition.X <= textRenderArea.Right)
                {
                    spriteBatch.DrawString(GraphicsManager.Instance.Font, "|", cursorPosition, TextColor, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();

            GraphicsDevice.ScissorRectangle = originalScissorRect;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        }
    }
}
