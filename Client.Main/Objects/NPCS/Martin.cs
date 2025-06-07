using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(248, "Wandering Merchant Martin")]
    public class Martin : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Merchant_Man.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
