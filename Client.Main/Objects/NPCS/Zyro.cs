using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(568, "Wandering Merchant Zyro")]
    public class Zyro : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/volvo.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
