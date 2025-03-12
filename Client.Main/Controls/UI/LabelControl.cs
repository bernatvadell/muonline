using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI
{

    public static class MatrixExtensions
    {
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
        private string _text;
        private object[] _textArgs = new object[0];
        private string _renderedText;
        private float _fontSize = 12f;
        private float _scaleFactor;

        // Nowe właściwości dla formatowania tekstu
        public bool IsBold { get; set; } = false;
        public bool IsItalic { get; set; } = false;
        public bool HasShadow { get; set; } = true;
        public bool HasUnderline { get; set; } = false;
        public float ShadowOpacity { get; set; } = 0.5f;
        public Vector2 ShadowOffset { get; set; } = new Vector2(1, 1);
        public Color ShadowColor { get; set; } = Color.Black;

        // Ustawienia pogrubienia (symulowane)
        public int BoldWeight { get; set; } = 1; // Ile pikseli offsetu dla symulacji pogrubienia

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
            if (!Visible || string.IsNullOrEmpty(_renderedText))
                return;

            SpriteBatch sprite = GraphicsManager.Instance.Sprite;

            Vector2 location = UseManualPosition
                ? new Vector2(X, Y)
                : DisplayRectangle.Location.ToVector2();

            if (TextAlign == HorizontalAlign.Center)
                location.X += (ViewSize.X - ControlSize.X) / 2;
            else if (TextAlign == HorizontalAlign.Right)
                location.X += ViewSize.X - ControlSize.X;

            location.Y += (ViewSize.Y - ControlSize.Y) / 2;

            // Calculate final text color with Alpha
            byte finalAlpha = (byte)(255 * Alpha);
            Color finalTextColor = new Color(TextColor.R, TextColor.G, TextColor.B, finalAlpha);

            float italicSkewFactor = IsItalic ? 0.2f : 0f;
            Vector2 textPosition = location;

            // Create a transformation matrix for italic text that doesn't affect Y positioning
            Matrix italicTransform = Matrix.Identity;
            if (IsItalic)
            {
                italicTransform = Matrix.CreateTranslation(-location.X, -location.Y, 0) *
                                  MatrixExtensions.CreateSkew(-italicSkewFactor, 0) * // Note: skewY is 0
                                  Matrix.CreateTranslation(location.X, location.Y, 0);
            }

            // Draw shadow
            if (HasShadow)
            {
                byte shadowA = (byte)(255 * ShadowOpacity * Alpha);
                Color shadowColor = new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, shadowA);
                Vector2 shadowPosition = textPosition + ShadowOffset;

                // Begin with proper transform matrix for shadow
                sprite.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                             null, null, null, null,
                             IsItalic ? italicTransform : Matrix.Identity);

                if (IsBold)
                {
                    // For shadow in bold mode, use less opacity for each layer
                    byte layerShadowAlpha = (byte)(shadowA / (2 * BoldWeight + 1));
                    Color layerShadowColor = new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, layerShadowAlpha);

                    for (int x = -BoldWeight; x <= BoldWeight; x++)
                    {
                        for (int y = -BoldWeight; y <= BoldWeight; y++)
                        {
                            if (x == 0 && y == 0) continue; // Skip central pixel

                            Vector2 offsetPosition = shadowPosition + new Vector2(x, y) * 0.5f;

                            sprite.DrawString(
                                GraphicsManager.Instance.Font,
                                _renderedText,
                                offsetPosition,
                                layerShadowColor,
                                0f, // no rotation
                                Vector2.Zero,
                                _scaleFactor,
                                SpriteEffects.None,
                                0f
                            );
                        }
                    }
                }
                else
                {
                    // Regular shadow
                    sprite.DrawString(
                        GraphicsManager.Instance.Font,
                        _renderedText,
                        shadowPosition,
                        shadowColor,
                        0f, // no rotation
                        Vector2.Zero,
                        _scaleFactor,
                        SpriteEffects.None,
                        0f
                    );
                }

                sprite.End();
            }

            // Draw main text
            sprite.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                         null, null, null, null,
                         IsItalic ? italicTransform : Matrix.Identity);

            if (IsBold)
            {
                // Bold text simulation with correct alpha
                byte layerAlpha = (byte)(finalAlpha / (2 * BoldWeight + 1));
                Color layerColor = new Color(TextColor.R, TextColor.G, TextColor.B, layerAlpha);

                for (int x = -BoldWeight; x <= BoldWeight; x++)
                {
                    for (int y = -BoldWeight; y <= BoldWeight; y++)
                    {
                        // Skip central pixel, will be added at the end
                        if (x == 0 && y == 0) continue;

                        Vector2 offsetPosition = textPosition + new Vector2(x, y) * 0.5f;

                        sprite.DrawString(
                            GraphicsManager.Instance.Font,
                            _renderedText,
                            offsetPosition,
                            layerColor,
                            0f, // no rotation
                            Vector2.Zero,
                            _scaleFactor,
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            // Draw main text always at the end (on top)
            sprite.DrawString(
                GraphicsManager.Instance.Font,
                _renderedText,
                textPosition,
                IsBold ? new Color(TextColor.R, TextColor.G, TextColor.B, (byte)(finalAlpha / (2 * BoldWeight + 1))) : finalTextColor,
                0f, // no rotation
                Vector2.Zero,
                _scaleFactor,
                SpriteEffects.None,
                0f
            );

            // Draw underline if enabled
            if (HasUnderline)
            {
                Vector2 textSize = GraphicsManager.Instance.Font.MeasureString(_renderedText) * _scaleFactor;
                float underlineThickness = Math.Max(1, _fontSize / 15);

                float underlineYOffset = textSize.Y * 0.9f;

                Vector2 underlineStart = new Vector2(textPosition.X, textPosition.Y + underlineYOffset);
                Vector2 underlineEnd = new Vector2(textPosition.X + textSize.X, textPosition.Y + underlineYOffset);

                // If text is italic, adjust underline position
                if (IsItalic)
                {
                    // Shift underline end for italic
                    underlineEnd.X += textSize.Y * italicSkewFactor;
                }

                // Draw underline using the pixel texture
                Texture2D pixel = GraphicsManager.Instance.GetPixelTexture();
                if (pixel != null)
                {
                    Vector2 delta = underlineEnd - underlineStart;
                    float angle = (float)Math.Atan2(delta.Y, delta.X);
                    float length = delta.Length();

                    sprite.Draw(
                        pixel,
                        underlineStart,
                        null,
                        finalTextColor,
                        angle,
                        Vector2.Zero,
                        new Vector2(length, underlineThickness),
                        SpriteEffects.None,
                        0
                    );
                }
            }

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