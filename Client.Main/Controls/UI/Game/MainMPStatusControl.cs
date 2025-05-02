using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainMPStatusControl : ProgressBarControl
    {
        public MainMPStatusControl()
        {
            TexturePath = "Interface/GFx/main_I1.ozd";
            Scale = 0.88f;
            Vertical = true;
            Inverse = true;
            Percentage = 0.75f;
            BlendState = BlendState.AlphaBlend;
        }
    }
}
