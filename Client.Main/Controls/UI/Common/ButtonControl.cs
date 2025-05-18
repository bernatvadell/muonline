using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers; // Dla GraphicsManager
using System.Text;
using System.Threading.Tasks;           // Dla StringBuilder

namespace Client.Main.Controls.UI.Common
{
    public class ButtonControl : SpriteControl // Inherit from SpriteControl
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
                    _truncatedText = null;
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
        private string _truncatedText;

        public int TextPaddingX { get; set; } = 2;
        public int TextPaddingY { get; set; } = 1;

        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font;
            Interactive = true;

            // SpriteControl's Load will call LoadTexture. 
            // If TexturePath is null/empty, SpriteControl.Texture will remain null.
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
                    sb.Remove(sb.Length - 1, 1);
                    break;
                }
            }
            return sb.ToString() + "...";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime); // This will call SpriteControl.Update

            // Update TileY only if a texture is present and TileHeight is valid
            if (this.Texture != null && this.TileHeight > 0)
            {
                int baseTileY = 0;
                if (!this.Enabled)
                {
                    baseTileY = (this.Texture.Height / this.TileHeight) > 3 ? 3 : 0;
                }
                else if (IsMousePressed) baseTileY = 2;
                else if (IsMouseOver) baseTileY = 1;

                int maxTileY = (this.Texture.Height / this.TileHeight) - 1;
                if (maxTileY < 0) maxTileY = 0;

                this.TileY = Math.Min(baseTileY, maxTileY);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            // If Texture is null, it means SpriteControl didn't load a texture (e.g., TexturePath was empty).
            // In this case, draw the colored background.
            if (this.Texture == null)
            {
                Color currentBgColor = base.BackgroundColor; // Use base.BackgroundColor to avoid recursion
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
                // DrawBorder(); // Optional: if texture-less buttons need a border
            }
            else
            {
                // If Texture is not null, SpriteControl's base.Draw will handle drawing it.
            }

            // Call base.Draw() AFTER drawing custom background (if any), 
            // so it draws the sprite (if texture exists) and then children.
            base.Draw(gameTime);

            // Draw text on top of everything (background or sprite)
            if (_font != null && !string.IsNullOrEmpty(Text))
            {
                if (_truncatedText == null)
                {
                    _truncatedText = TruncateTextToFit(Text, DisplaySize.X - (TextPaddingX * 2));
                }

                Color currentTextColor = Enabled ? (IsMouseOver ? HoverTextColor : TextColor) : DisabledTextColor;
                float scale = FontSize / Constants.BASE_FONT_SIZE; // Use defined constant
                Vector2 textSize = _font.MeasureString(_truncatedText) * scale;

                // Center text within the button's display rectangle
                Vector2 textPosition = new Vector2(
                    DisplayPosition.X + (DisplaySize.X - textSize.X) / 2,
                    DisplayPosition.Y + (DisplaySize.Y - textSize.Y) / 2
                );
                GraphicsManager.Instance.Sprite.DrawString(_font, _truncatedText, textPosition, currentTextColor * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        public override bool OnClick()
        {
            if (Enabled)
            {
                base.OnClick();
                return true;
            }
            return false;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            _truncatedText = null;
        }
    }
}