using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(241, "Guild Master")]
    public class GuildMaster : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Master01.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
