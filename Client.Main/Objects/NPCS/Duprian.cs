using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(543, "Gens Duprian Steward")]
    public class Duprian : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/duprian.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
