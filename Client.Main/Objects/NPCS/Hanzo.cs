using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(251, "Hanzo The Blacksmith")]
    public class Hanzo : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Smith01.bmd");
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
