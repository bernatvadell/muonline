using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(368, "Elpis")]
    public class Elpis : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Smelting_Npc.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
