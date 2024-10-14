using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerMaskHelmObject : ModelObject
    {
        public PlayerMaskHelmObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/MaskHelmMale412.bmd");
            await base.Load();
        }
    }
}
