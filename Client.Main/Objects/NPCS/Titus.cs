using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(479, "Gatekeeper Titus")]
    public class Titus : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Duel_Npc.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
