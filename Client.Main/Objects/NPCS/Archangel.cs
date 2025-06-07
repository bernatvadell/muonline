using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(232, "Archangel")]
    public class Archangel : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Archangel.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
