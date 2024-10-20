using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerGloveObject : ModelObject
    {
        public int PlayerClass { get; set; }
        public PlayerGloveObject(int playerClass)
        {
            PlayerClass = playerClass;
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Player/GloveClass{PlayerClass:D2}.bmd");
            await base.Load();
        }
    }
}
