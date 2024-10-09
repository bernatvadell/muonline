using Client.Main.Controls;
using Client.Main.Worlds;

namespace Client.Main.Scenes
{
    public class SelectCharacterScene : BaseScene
    {
        public SelectCharacterScene()
        {
            ChangeWorld<SelectCharacterWorld>();
        }
    }
}
