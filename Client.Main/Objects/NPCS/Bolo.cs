using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(578, "Weapons Merchant Bolo")]
    public class Bolo : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Karutan_Npc_Volvo.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
