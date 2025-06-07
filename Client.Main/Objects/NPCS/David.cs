using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(579, "David")]
    public class David : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/LuckyItem_Npc.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
