using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(259, "Oracle Layla")]
    public class Layla : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Kalima_Shop.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
