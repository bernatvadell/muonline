using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI
{
    public class SpriteControl : TextureControl
    {
        public int ElementWidth { get; set; }
        public int ElementHeight { get; set; }
        public int Position { get; set; }
        public override Rectangle SourceRectangle => new(0, Position * ElementHeight, ElementWidth, ElementHeight);
    }
}
