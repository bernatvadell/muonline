using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerBootObject : ModelObject
    {
        public PlayerBootObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/BootClass01.bmd");
            await base.Load();
        }
    }
}
