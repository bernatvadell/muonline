using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(254, "Pasi, the Mage")]
    public class Pasi : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Wizard01.bmd");
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
