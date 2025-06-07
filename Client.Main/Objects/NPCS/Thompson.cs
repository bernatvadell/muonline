using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(231, "Thompson the Merchant")]
    public class Thompson : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Devias_Trader.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
