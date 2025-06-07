using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(577, "Leina the General Goods Merchant")]
    public class Leina : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Karutan_Npc_Reina.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
