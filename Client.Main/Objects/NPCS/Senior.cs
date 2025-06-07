using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(223, "Senior")]
    public class Senior : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Man01.bmd");
            await SetBodyPartsAsync("Npc/", "ManHead", "ManUpper", "ManPant", "ManGlove", "ManBoots", 1);
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
