using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI
{
    public class LabelControl : UIControl
    {
        private string _text;
        private object[] _textArgs = new object[0];
        private string _renderedText;
        private float _fontSize = 12f;
        private float _scaleFactor;

        public bool UseManualPosition { get; set; } = false;

        /// <summary>
        /// Text to display. Changing this recalculates the rendered text.
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnChangeText();
                }
            }
        }

        /// <summary>
        /// Format arguments.
        /// </summary>
        public object[] TextArgs
        {
            get => _textArgs;
            set
            {
                if (_textArgs != value)
                {
                    _textArgs = value;
                    OnChangeText();
                }
            }
        }

        /// <summary>
        /// Desired font size on screen.
        /// </summary>
        public float FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnChangeText();
                }
            }
        }

        public float Alpha { get; set; } = 1f;

        public Color TextColor { get; set; } = Color.WhiteSmoke;
        public HorizontalAlign TextAlign { get; set; } = HorizontalAlign.Left;

        public override void Draw(GameTime gameTime)
        {
            if (!Visible)
                return;

            SpriteBatch sprite = GraphicsManager.Instance.Sprite;
            sprite.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            Vector2 location = UseManualPosition
                ? new Vector2(X, Y)
                : DisplayRectangle.Location.ToVector2();

            if (TextAlign == HorizontalAlign.Center)
                location.X += (ViewSize.X - ControlSize.X) / 2;
            else if (TextAlign == HorizontalAlign.Right)
                location.X += ViewSize.X - ControlSize.X;

            location.Y += (ViewSize.Y - ControlSize.Y) / 2;

            Vector2 shadowOffset = new Vector2(1, 1);
            byte shadowA = (byte)(255 * 0.5f * Alpha);
            Color shadowColor = new Color((float)0, 0, 0, shadowA);
            sprite.DrawString(
                GraphicsManager.Instance.Font,
                _renderedText,
                location + shadowOffset,
                shadowColor,
                0f,
                Vector2.Zero,
                _scaleFactor,
                SpriteEffects.None,
                0f
            );

            byte finalAlpha = (byte)(255 * Alpha);
            Color finalTextColor = new Color(TextColor.R, TextColor.G, TextColor.B, finalAlpha);
            sprite.DrawString(
                GraphicsManager.Instance.Font,
                _renderedText,
                location,
                finalTextColor,
                0f,
                Vector2.Zero,
                _scaleFactor,
                SpriteEffects.None,
                0f
            );

            sprite.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);
        }

        private void OnChangeText()
        {
            if (string.IsNullOrEmpty(Text))
            {
                ControlSize = Point.Zero;
                return;
            }

            _renderedText = SafeFormat(Text, TextArgs);
            _scaleFactor = FontSize / Constants.BASE_FONT_SIZE;

            var textSize = GraphicsManager.Instance.Font.MeasureString(_renderedText);
            ControlSize = new Point(
                (int)(textSize.X * _scaleFactor),
                (int)(textSize.Y * _scaleFactor)
            );
        }

        private string SafeFormat(string format, params object[] args)
        {
            if (args == null || args.Length == 0)
                return format;

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
