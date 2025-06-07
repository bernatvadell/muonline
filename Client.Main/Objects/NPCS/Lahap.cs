using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(256, "Lahap")]
    public class Lahap : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Serbis.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
