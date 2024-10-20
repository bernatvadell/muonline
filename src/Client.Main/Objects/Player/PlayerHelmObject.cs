using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerHelmObject : ModelObject
    {
        public int PlayerClass { get; set; }
        public PlayerHelmObject(int playerClass)
        {
            PlayerClass = playerClass;
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Player/HelmClass{PlayerClass:D2}.bmd");
            await base.Load();
        }
    }
}
