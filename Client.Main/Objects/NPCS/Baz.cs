using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(240, "Baz The Vault Keeper")]
    public class Baz : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Storage01.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
