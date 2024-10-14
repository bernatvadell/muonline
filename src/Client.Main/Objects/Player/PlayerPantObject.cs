using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerPantObject : ModelObject
    {
        public PlayerPantObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Item/PantMale4005.bmd");
            await base.Load();
        }
    }
}
