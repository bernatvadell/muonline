using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Controls.UI
{
    public class ProgressBarControl : TextureControl
    {
        public float Percentage { get; set; }
        public bool Vertical { get; set; }
        public bool Inverse { get; set; }

        public ProgressBarControl()
        {
            AutoViewSize = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible || Texture == null || Texture.IsDisposed)
            {
                if (ViewSize != Point.Zero) ViewSize = Point.Zero;
                if (TextureRectangle != Rectangle.Empty) TextureRectangle = Rectangle.Empty;
                return;
            }

            float sourceWidth = Texture.Width;
            float sourceHeight = Texture.Height;
            float clampedPercentage = Math.Clamp(Percentage, 0f, 1f);
            float calculatedWidth = sourceWidth * clampedPercentage;
            float calculatedHeight = sourceHeight * clampedPercentage;
            Rectangle currentTextureRect;

            if (!Vertical)
            {
                sourceWidth = calculatedWidth;
                if (Inverse) currentTextureRect = new Rectangle((int)(Texture.Width - sourceWidth), 0, (int)sourceWidth, (int)Texture.Height);
                else currentTextureRect = new Rectangle(0, 0, (int)sourceWidth, (int)Texture.Height);
                ViewSize = new Point((int)(sourceWidth * Scale), (int)(Texture.Height * Scale));
            }
            else
            {
                sourceHeight = calculatedHeight;
                if (Inverse) currentTextureRect = new Rectangle(0, (int)(Texture.Height - sourceHeight), (int)Texture.Width, (int)sourceHeight);
                else currentTextureRect = new Rectangle(0, 0, (int)Texture.Width, (int)sourceHeight);
                ViewSize = new Point((int)(Texture.Width * Scale), (int)(sourceHeight * Scale));
            }

            TextureRectangle = Rectangle.Intersect(currentTextureRect, Texture.Bounds);
            ViewSize = new Point(Math.Max(0, ViewSize.X), Math.Max(0, ViewSize.Y));
        }
    }
}