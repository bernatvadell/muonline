using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(408, "Gatekeeper")]
    public class Gatekeeper : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Castel_Gate.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
