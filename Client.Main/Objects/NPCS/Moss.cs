using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(492, "Moss The Gambler")]
    public class Moss : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Gamble_Npc_Moss.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
