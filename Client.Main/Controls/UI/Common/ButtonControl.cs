using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers; // Dla GraphicsManager
using Client.Main.Helpers;    // Dla SpriteBatchScope
using System.Text;
using System.Threading.Tasks;           // Dla StringBuilder

namespace Client.Main.Controls.UI.Common
{
    public class ButtonControl : UIControl
    {
        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    _truncatedText = null; // Wymuś ponowne obliczenie przyciętego tekstu
                }
            }
        }

        public float FontSize { get; set; } = 12f;
        public Color TextColor { get; set; } = Color.White;
        public Color HoverTextColor { get; set; } = Color.Yellow;
        public Color DisabledTextColor { get; set; } = Color.DarkGray;
        public Color HoverBackgroundColor { get; set; } = new Color(80, 80, 120, 200);
        public Color PressedBackgroundColor { get; set; } = new Color(60, 60, 100, 220);
        public bool Enabled { get; set; } = true;

        private SpriteFont _font;
        private string _truncatedText; // Przechowuje przyciętą wersję tekstu

        // Odstępy tekstu wewnątrz przycisku
        public int TextPaddingX { get; set; } = 2;
        public int TextPaddingY { get; set; } = 1;


        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font;
            Interactive = true;
            await base.Load();
        }

        private string TruncateTextToFit(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrEmpty(text) || maxWidth <= 0)
                return string.Empty;

            float scale = FontSize / Constants.BASE_FONT_SIZE;
            if (_font.MeasureString(text).X * scale <= maxWidth)
                return text;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i]);
                if (_font.MeasureString(sb.ToString() + "...").X * scale > maxWidth)
                {
                    sb.Remove(sb.Length - 1, 1); // Usuń ostatni znak, który przekroczył
                    break;
                }
            }
            return sb.ToString() + "...";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Visual state depends on IsMouseOver and IsMousePressed,
            // which are correctly set by GameControl.Update's internal logic.
            // No need to check Scene.MouseControl here for hover.
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            Color currentBgColor = BackgroundColor;
            if (!Enabled)
            {
                currentBgColor = Color.DarkSlateGray * 0.8f * Alpha;
            }
            else if (IsMousePressed)
            {
                currentBgColor = PressedBackgroundColor * Alpha;
            }
            else if (IsMouseOver)
            {
                currentBgColor = HoverBackgroundColor * Alpha;
            }
            else
            {
                currentBgColor *= Alpha;
            }

            if (currentBgColor.A > 0)
            {
                GraphicsManager.Instance.Sprite.Draw(GraphicsManager.Instance.Pixel, DisplayRectangle, currentBgColor);
            }

            // ---- DODAJ TYMCZASOWE OBRAMOWANIE DO DEBUGOWANIA ----
            if (Enabled)
                DrawRectangle(DisplayRectangle, IsMouseOver ? Color.Lime : Color.Red, 1); // Zielone gdy hover, czerwone normalnie
            else
                DrawRectangle(DisplayRectangle, Color.DimGray, 1);
            // ---- KONIEC TYMCZASOWEGO OBRAMOWANIA ----

            // DrawBorder(); // Twoje standardowe obramowanie (jeśli jest)

            if (_font != null && !string.IsNullOrEmpty(Text))
            {
                if (_truncatedText == null)
                {
                    _truncatedText = TruncateTextToFit(Text, DisplaySize.X - (TextPaddingX * 2));
                }

                Color currentTextColor = Enabled ? (IsMouseOver ? HoverTextColor : TextColor) : DisabledTextColor;
                float scale = FontSize / Constants.BASE_FONT_SIZE;
                Vector2 textSize = _font.MeasureString(_truncatedText) * scale;

                Vector2 textPosition = new Vector2(
                    DisplayPosition.X + TextPaddingX,
                    DisplayPosition.Y + (DisplaySize.Y - textSize.Y) / 2
                );
                GraphicsManager.Instance.Sprite.DrawString(_font, _truncatedText, textPosition, currentTextColor * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        // Metoda pomocnicza do rysowania prostokąta (dla debugowania)
        private void DrawRectangle(Rectangle rect, Color color, int thickness)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            var spriteBatch = GraphicsManager.Instance.Sprite;
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        public override bool OnClick()
        {
            if (Enabled)
            {
                base.OnClick(); // fires the Click event
                return true;    // button always consumes the click if enabled
            }
            return false;
        }

        // Wywołaj, gdy rozmiar przycisku się zmienia, aby przeliczyć tekst
        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            _truncatedText = null;
        }
    }
}
// --- END OF FILE UI/Common/ButtonControl.cs ---