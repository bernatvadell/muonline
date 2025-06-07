using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(238, "Chaos Goblin")]
    public class ChaosGoblin : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/MixNpc01.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
