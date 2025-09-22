using Client.Main;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI
{
    public abstract class UIControl : GameControl
    {
        protected override MouseState CurrentMouseState => MuGame.Instance.UiMouseState;
        protected override MouseState PreviousMouseState => MuGame.Instance.PrevUiMouseState;

        public UIControl()
        {
        }
    }
}
