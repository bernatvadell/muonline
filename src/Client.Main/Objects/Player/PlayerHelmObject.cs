using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerHelmObject : ModelObject
    {
        public PlayerHelmObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/HelmClass01.bmd");
            await base.Load();
        }
    }
}
