using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    public class TextFieldControl : UIControl
    {
        private Texture2D _cornerTopLeftTexture;
        private Texture2D _topLineTexture;
        private Texture2D _cornerTopRightTexture;
        private Texture2D _leftLineTexture;
        private Texture2D _backgroundTexture;
        private Texture2D _rightLineTexture;
        private Texture2D _cornerBottomLeftTexture;
        private Texture2D _bottomLineTexture;
        private Texture2D _cornerBottomRightTexture;
        private readonly StringBuilder _inputText = new();
        private double _cursorBlinkTimer;
        private bool _showCursor;
        private float _scrollOffset;

        private const int TextMargin = 10;
        private const int CursorBlinkInterval = 500;

        public TextFieldControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(170, 25);
            Interactive = true;
        }

        public string Value
        {
            get => _inputText.ToString();
            set
            {
                _inputText.Clear();
                _inputText.Append(value);
            }
        }

        public bool MaskValue { get; set; }
        public event EventHandler ValueChanged;

        public override async Task Load()
        {
            await base.Load();

            var windowName = "textbg";

            _cornerTopLeftTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}01.ozd");
            _topLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}02.ozd");
            _cornerTopRightTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}03.ozd");
            _leftLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}04.ozd");
            _backgroundTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}05.ozd");
            _rightLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}06.ozd");
            _cornerBottomLeftTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}07.ozd");
            _bottomLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}08.ozd");
            _cornerBottomRightTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}09.ozd");
        }

        public override void OnBlur()
        {
            base.OnBlur();
            _showCursor = false;
            _cursorBlinkTimer = 0;
        }

        public override void OnFocus()
        {
            base.OnFocus();
            _showCursor = true;
            _cursorBlinkTimer = 0;
        }

        private void ProcessKey(Keys key, bool shift, bool capsLock)
        {
            if (key == Keys.Back && _inputText.Length > 0)
            {
                _inputText.Remove(_inputText.Length - 1, 1);
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
                }
            }
        }

        private char KeyToChar(Keys key, bool shift, bool capsLock)
        {
            bool isUpper = capsLock ^ shift;
            string keyStr = key.ToString();
            char character;

            if (keyStr.Length == 1 && char.IsLetterOrDigit(keyStr[0]))
            {
                character = isUpper ? keyStr[0] : char.ToLower(keyStr[0]);
            }
            else
            {
                character = key switch
                {
                    Keys.Space => ' ',
                    Keys.OemComma => ',',
                    Keys.OemPeriod => '.',
                    Keys.OemMinus => shift ? '_' : '-',
                    Keys.OemPlus => shift ? '+' : '=',
                    _ => '\0'
                };
            }

            return character;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!HasFocus) return;

            var keysPressed = MuGame.Instance.Keyboard.GetPressedKeys();
            var shift = MuGame.Instance.Keyboard.IsKeyDown(Keys.LeftShift) || MuGame.Instance.Keyboard.IsKeyDown(Keys.RightShift);
            var capsLock = Console.CapsLock;

            foreach (var key in keysPressed)
            {
                if (MuGame.Instance.PrevKeyboard.IsKeyUp(key))
                {
                    ProcessKey(key, shift, capsLock);
                }
            }

            _cursorBlinkTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (_cursorBlinkTimer >= CursorBlinkInterval)
            {
                _showCursor = !_showCursor;
                _cursorBlinkTimer = 0;
            }

            var textWidth = GraphicsManager.Instance.Font.MeasureString(MaskValue ? new string('*', _inputText.Length) : _inputText.ToString()).X;
            float maxVisibleWidth = DisplayRectangle.Width - TextMargin * 2;

            if (textWidth > maxVisibleWidth)
            {
                _scrollOffset = textWidth - maxVisibleWidth;
            }
            else
            {
                _scrollOffset = 0;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            var sprite = GraphicsManager.Instance.Sprite;

            sprite.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.Default,
                RasterizerState.CullNone,
                GraphicsManager.Instance.AlphaTestEffectUI
            );

            sprite.Draw(_cornerTopLeftTexture, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y, _cornerTopLeftTexture.Width, _cornerTopLeftTexture.Height), Color.White);
            sprite.Draw(_cornerTopRightTexture, new Rectangle(DisplayRectangle.X + DisplayRectangle.Width - _cornerTopRightTexture.Width, DisplayRectangle.Y, _cornerTopRightTexture.Width, _cornerTopRightTexture.Height), Color.White);
            sprite.Draw(_cornerBottomLeftTexture, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y + DisplayRectangle.Height - _cornerBottomLeftTexture.Height, _cornerBottomLeftTexture.Width, _cornerBottomLeftTexture.Height), Color.White);
            sprite.Draw(_cornerBottomRightTexture, new Rectangle(DisplayRectangle.X + DisplayRectangle.Width - _cornerBottomRightTexture.Width, DisplayRectangle.Y + DisplayRectangle.Height - _cornerBottomRightTexture.Height, _cornerBottomRightTexture.Width, _cornerBottomRightTexture.Height), Color.White);
            sprite.Draw(_topLineTexture, new Rectangle(DisplayRectangle.X + _cornerTopLeftTexture.Width, DisplayRectangle.Y, DisplayRectangle.Width - _cornerTopLeftTexture.Width - _cornerTopRightTexture.Width, _topLineTexture.Height), Color.White);
            sprite.Draw(_bottomLineTexture, new Rectangle(DisplayRectangle.X + _cornerBottomLeftTexture.Width, DisplayRectangle.Y + DisplayRectangle.Height - _bottomLineTexture.Height, DisplayRectangle.Width - _cornerBottomLeftTexture.Width - _cornerBottomRightTexture.Width, _bottomLineTexture.Height), Color.White);
            sprite.Draw(_leftLineTexture, new Rectangle(DisplayRectangle.X, DisplayRectangle.Y + _cornerTopLeftTexture.Height, _leftLineTexture.Width, DisplayRectangle.Height - _cornerTopLeftTexture.Height - _cornerBottomLeftTexture.Height), Color.White);
            sprite.Draw(_rightLineTexture, new Rectangle(DisplayRectangle.X + DisplayRectangle.Width - _rightLineTexture.Width, DisplayRectangle.Y + _cornerTopRightTexture.Height, _rightLineTexture.Width, DisplayRectangle.Height - _cornerTopRightTexture.Height - _cornerBottomRightTexture.Height), Color.White);
            sprite.Draw(_backgroundTexture, new Rectangle(DisplayRectangle.X + _leftLineTexture.Width, DisplayRectangle.Y + _topLineTexture.Height, DisplayRectangle.Width - _leftLineTexture.Width - _rightLineTexture.Width, DisplayRectangle.Height - _topLineTexture.Height - _bottomLineTexture.Height), Color.White);

            sprite.End();

            GraphicsDevice.ScissorRectangle = new Rectangle(DisplayRectangle.X + TextMargin, DisplayRectangle.Y, DisplayRectangle.Width - TextMargin * 2, DisplayRectangle.Height);

            sprite.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.Default,
                new RasterizerState { ScissorTestEnable = true },
                null
            );

            var textToDisplay = MaskValue ? new string('*', _inputText.Length) : _inputText.ToString();
            var textPosition = new Vector2(DisplayRectangle.X + TextMargin - _scrollOffset, DisplayRectangle.Y + (DisplayRectangle.Height - GraphicsManager.Instance.Font.LineSpacing) / 2);
            sprite.DrawString(GraphicsManager.Instance.Font, textToDisplay, textPosition, Color.White);

            if (HasFocus && _showCursor)
            {
                var textWidth = GraphicsManager.Instance.Font.MeasureString(textToDisplay).X;
                var cursorPosition = textPosition + new Vector2(textWidth, 0);
                sprite.DrawString(GraphicsManager.Instance.Font, "|", cursorPosition, Color.White);
            }

            sprite.End();

            base.Draw(gameTime);
        }
    }
}
