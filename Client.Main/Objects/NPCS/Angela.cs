using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(377, "Angela the Supplier")]
    public class Angela : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/bc_npc2.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
