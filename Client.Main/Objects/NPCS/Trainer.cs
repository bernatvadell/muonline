using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(226, "Pet Trainer")]
    public class Trainer : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Breeder.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
