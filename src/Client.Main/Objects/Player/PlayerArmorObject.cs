using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerArmorObject : ModelObject
    {
        public PlayerArmorObject()
        {
            RenderShadow = true;
        }


        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/ArmorClass01.bmd");
            await base.Load();
        }
    }
}
