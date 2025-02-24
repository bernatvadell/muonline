using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (!Visible) return;

            float sourceWidth = Texture.Width;
            float sourceHeight = Texture.Height;

            if (!Vertical)
            {
                sourceWidth = Texture.Width * Percentage;
                if (Inverse) TextureRectangle = new Rectangle((int)(Texture.Width - sourceWidth), 0, (int)sourceWidth, Texture.Height);
                else TextureRectangle = new Rectangle(0, 0, (int)sourceWidth, Texture.Height);
            }

            if (Vertical)
            {
                sourceHeight = (int)(Texture.Height * Percentage);
                if (Inverse) TextureRectangle = new Rectangle(0, (int)(Texture.Height - sourceHeight), Texture.Width, (int)sourceHeight);
                else TextureRectangle = new Rectangle(0, 0, Texture.Width, (int)sourceHeight);
            }

            ViewSize = new Point(
                (int)sourceWidth,
                (int)sourceHeight
            );

            base.Update(gameTime);
        }
    }
}
