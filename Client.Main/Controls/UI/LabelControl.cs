using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI
{
    public static class MatrixExtensions
    {
        // Methods
        public static Matrix CreateSkew(float skewX, float skewY)
        {
            // Creates a 2D skew matrix.
            return new Matrix(
                1, skewY, 0, 0,
                skewX, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            );
        }
    }

    public class LabelControl : UIControl
    {
        // Fields
        private string _text;
        private object[] _textArgs = Array.Empty<object>();
        private string _renderedText;
        private float _fontSize = 12f;
        private float _scaleFactor;

        // Properties

        // New properties for text formatting
        public bool IsBold { get; set; } = false;
        public bool IsItalic { get; set; } = false;
        public bool HasShadow { get; set; } = true;
        public bool HasUnderline { get; set; } = false;
        public float ShadowOpacity { get; set; } = 0.5f;
        public Vector2 ShadowOffset { get; set; } = new Vector2(1, 1);
        public Color ShadowColor { get; set; } = Color.Black;

        // Bold settings (simulated)
        public int BoldWeight { get; set; } = 1; // How many pixels offset for bold simulation

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

        public new float Alpha { get; set; } = 1f;

        public Color TextColor { get; set; } = Color.WhiteSmoke;
        public HorizontalAlign TextAlign { get; set; } = HorizontalAlign.Left;

        // Methods
        public override void Draw(GameTime gameTime)
        {
            if (!Visible || string.IsNullOrEmpty(_renderedText))
                return;

            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            sb.End();

            Vector2 origin;
            if (UseManualPosition)
            {
                origin = new Vector2(X, Y);
            }
            else
            {
                origin = DisplayRectangle.Location.ToVector2();
                if (TextAlign == HorizontalAlign.Center)
                    origin.X += (ViewSize.X - ControlSize.X) * 0.5f;
                else if (TextAlign == HorizontalAlign.Right)
                    origin.X += (ViewSize.X - ControlSize.X);
                origin.Y += (ViewSize.Y - ControlSize.Y) * 0.5f;
            }

            // Create a transformation matrix for italic text that doesn't affect Y positioning
            Matrix italicTransform = Matrix.Identity;
            if (IsItalic)
            {
                const float skew = 0.2f;
                italicTransform =
                    Matrix.CreateTranslation(-origin.X, -origin.Y, 0f) *
                    MatrixExtensions.CreateSkew(-skew, 0f) *
                    Matrix.CreateTranslation(origin.X, origin.Y, 0f);
            }

            // Draw shadow
            if (HasShadow)
            {
                sb.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.NonPremultiplied,
                    null, null, null, null,
                    italicTransform);

                var shadowA = (byte)(255 * ShadowOpacity * Alpha);
                var shadowCol = new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, shadowA);
                var shadowPos = origin + ShadowOffset;

                if (IsBold)
                {
                    byte layerA = (byte)(shadowA / (2 * BoldWeight + 1));
                    var layerCol = new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, layerA);
                    for (int dx = -BoldWeight; dx <= BoldWeight; dx++)
                        for (int dy = -BoldWeight; dy <= BoldWeight; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            sb.DrawString(
                                font,
                                _renderedText,
                                shadowPos + new Vector2(dx, dy) * 0.5f,
                                layerCol,
                                0f, Vector2.Zero,
                                _scaleFactor,
                                SpriteEffects.None,
                                0f);
                        }
                }
                else
                {
                    sb.DrawString(
                        font,
                        _renderedText,
                        shadowPos,
                        shadowCol,
                        0f, Vector2.Zero,
                        _scaleFactor,
                        SpriteEffects.None,
                        0f);
                }

                sb.End();
            }

            // Draw main text
            sb.Begin(
                SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                null, null, null, null,
                italicTransform);

            if (IsBold)
            {
                byte layerA = (byte)(255 * Alpha / (2 * BoldWeight + 1));
                var layerCol = new Color(TextColor.R, TextColor.G, TextColor.B, layerA);
                for (int dx = -BoldWeight; dx <= BoldWeight; dx++)
                    for (int dy = -BoldWeight; dy <= BoldWeight; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        sb.DrawString(
                            font,
                            _renderedText,
                            origin + new Vector2(dx, dy) * 0.5f,
                            layerCol,
                            0f, Vector2.Zero,
                            _scaleFactor,
                            SpriteEffects.None,
                            0f);
                    }
            }

            var mainCol = new Color(TextColor.R, TextColor.G, TextColor.B, (byte)(255 * Alpha));
            sb.DrawString(
                font,
                _renderedText,
                origin,
                mainCol,
                0f, Vector2.Zero,
                _scaleFactor,
                SpriteEffects.None,
                0f);

            // Draw underline if enabled
            if (HasUnderline)
            {
                var textSize = font.MeasureString(_renderedText) * _scaleFactor;
                float thick = Math.Max(1, _fontSize / 15f);
                Vector2 start = origin + new Vector2(0, textSize.Y * 0.9f);
                Vector2 end = start + new Vector2(
                    textSize.X + (IsItalic ? textSize.Y * 0.2f : 0f),
                    0f);

                var pixel = GraphicsManager.Instance.GetPixelTexture();
                if (pixel != null)
                {
                    var delta = end - start;
                    float angle = (float)Math.Atan2(delta.Y, delta.X);
                    float len = delta.Length();
                    sb.Draw(
                        pixel,
                        start,
                        null,
                        mainCol,
                        angle,
                        Vector2.Zero,
                        new Vector2(len, thick),
                        SpriteEffects.None,
                        0f);
                }
            }

            sb.End();

            sb.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp);

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

            // Adjust control size considering formatting effects
            float widthMultiplier = 1.0f;
            float heightMultiplier = 1.0f;

            if (IsBold) widthMultiplier += BoldWeight * 0.1f;
            if (IsItalic)
            {
                widthMultiplier += 0.1f;
            }

            ControlSize = new Point(
                (int)(textSize.X * _scaleFactor * widthMultiplier),
                (int)(textSize.Y * _scaleFactor * heightMultiplier)
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