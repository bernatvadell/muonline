using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(243, "Eo the Craftsman")]
    public class EoTheCraftsman : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/ElfMerchant01.bmd");
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
