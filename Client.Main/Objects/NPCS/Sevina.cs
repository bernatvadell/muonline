using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(235, "Priestess Sevina")]
    public class Sevina : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Sevina.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
