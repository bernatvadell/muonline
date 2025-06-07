using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(406, "Priest Devin")]
    public class PriestDevin : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Devin.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
