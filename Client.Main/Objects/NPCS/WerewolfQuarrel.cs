using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(407, "Werewolf Quarrel")]
    public class WerewolfQuarrel : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Quarrel.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
