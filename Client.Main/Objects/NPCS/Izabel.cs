using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(245, "Izabel The Wizard")]
    public class Izabel : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/SnowWizard1.bmd");
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
