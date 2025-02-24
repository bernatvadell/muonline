using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    public class EoTheCraftsman : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/ElfMerchant01.bmd");
            await base.Load();
        }
    }
}
