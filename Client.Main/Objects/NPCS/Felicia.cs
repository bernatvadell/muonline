using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(566, "Mercenary Guild Felicia")]
    public class Felicia : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Tersia.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
