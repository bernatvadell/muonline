using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(567, "Priestess Veina")]
    public class Veina : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Bena.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
