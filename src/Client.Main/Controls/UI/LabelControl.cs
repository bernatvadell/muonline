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

        public Color TextColor { get; set; } = Color.WhiteSmoke;
        public HorizontalAlign TextAlign { get; set; } = HorizontalAlign.Left;

        public override void Draw(GameTime gameTime)
        {
            GraphicsManager.Instance.Sprite.Begin();

            // Choose the position source: manual or using DisplayRectangle.
            Vector2 location = UseManualPosition
                ? new Vector2(X, Y)
                : DisplayRectangle.Location.ToVector2();

            // Adjust for alignment if needed.
            if (TextAlign == HorizontalAlign.Center)
                location.X += (ViewSize.X - ControlSize.X) / 2;
            else if (TextAlign == HorizontalAlign.Right)
                location.X += ViewSize.X - ControlSize.X;

            location.Y += (ViewSize.Y - ControlSize.Y) / 2;

            // Draw shadow (offset by 1px)
            var shadowOffset = new Vector2(1, 1);
            var shadowColor = Color.Black * 0.5f;
            byte newAlpha = (byte)(shadowColor.A * 0.5);
            Color newShadowColor = new Color(shadowColor.R, shadowColor.G, shadowColor.B, newAlpha);
            GraphicsManager.Instance.Sprite.DrawString(
                GraphicsManager.Instance.Font,
                _renderedText,
                location + shadowOffset,
                newShadowColor,
                0f,
                Vector2.Zero,
                _scaleFactor,
                SpriteEffects.None,
                0f
            );

            byte newTextAlpha = (byte)(TextColor.A * 0.8);
            Color newTextColor = new Color(TextColor.R, TextColor.G, TextColor.B, newTextAlpha);
            // Draw main text
            GraphicsManager.Instance.Sprite.DrawString(
                GraphicsManager.Instance.Font,
                _renderedText,
                location,
                newTextColor,
                0f,
                Vector2.Zero,
                _scaleFactor,
                SpriteEffects.None,
                0f
            );

            GraphicsManager.Instance.Sprite.End();

            // Restore graphic states.
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
