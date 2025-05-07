using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MainHPStatusControl : ProgressBarControl
    {
        public MainHPStatusControl()
        {
            TexturePath = "Interface/GFx/main_I3.ozd";
            Scale = 0.88f;
            Vertical = true;
            Inverse = true;
            Percentage = 0.75f;
            BlendState = BlendState.AlphaBlend;
        }
    }
}
