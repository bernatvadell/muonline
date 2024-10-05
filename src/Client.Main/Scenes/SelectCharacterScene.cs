using Client.Main.Controls;
using Client.Main.Worlds;

namespace Client.Main.Scenes
{
    public class SelectCharacterScene : GameControl
    {
        public SelectCharacterScene()
        {
            Controls.Add(new SelectCharacterWorld());
        }
    }
}
