using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(379, "Fireworks Girl")]
    public class FireworksGirl : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Wedding_Npc.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
