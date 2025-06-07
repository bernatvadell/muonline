using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(376, "Pamela the Supplier")]
    public class Pamela : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/bc_npc1.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
