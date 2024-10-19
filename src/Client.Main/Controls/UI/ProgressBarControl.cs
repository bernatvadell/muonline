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
        private Rectangle _sourceRectangle;
        public float Percentage { get; set; }
        public bool Vertical { get; set; }
        public bool Inverse { get; set; }

        public override Rectangle SourceRectangle => _sourceRectangle;

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            int sourceWidth = Texture.Width;
            int sourceHeight = Texture.Height;

            if (!Vertical)
                sourceWidth = (int)(Texture.Width * Percentage);

            if (Vertical)
                sourceHeight = (int)(Texture.Height * Percentage);

            _sourceRectangle = new Rectangle(
                Inverse && !Vertical ? Texture.Width - sourceWidth : OffsetX,
                Inverse && Vertical ? Texture.Height - sourceHeight : OffsetY,
                sourceWidth,
                sourceHeight
            );

            Width = sourceWidth;
            Height = sourceHeight;

            base.Update(gameTime);
        }
    }
}
