using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Controls.UI.Common
{
    public class ColorBarControl : UIControl
    {
        public float Percentage { get; set; }
        public Color FillColor { get; set; } = Color.Red;

        public ColorBarControl()
        {
            AutoViewSize = false;
            Interactive = false;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || DisplayRectangle.Width <= 0 || DisplayRectangle.Height <= 0)
                return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            var fullRect = DisplayRectangle;

            if (BackgroundColor.A > 0)
            {
                spriteBatch.Draw(pixel, fullRect, BackgroundColor * Alpha);
            }

            if (Percentage > 0)
            {
                float clampedPercentage = Math.Clamp(Percentage, 0f, 1f);
                int fillWidth = (int)(fullRect.Width * clampedPercentage);

                if (fillWidth > 0)
                {
                    Rectangle fillRect = new Rectangle(fullRect.X, fullRect.Y, fillWidth, fullRect.Height);
                    spriteBatch.Draw(pixel, fillRect, FillColor * Alpha);
                }
            }

            DrawBorder();
        }
    }
}