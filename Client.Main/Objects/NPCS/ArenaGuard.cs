using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(239, "Arena Guard")]
    public class ArenaGuard : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Tournament.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
