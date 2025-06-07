using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(224, "Guardsman")]
    public class Guardsman : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Clerk.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
