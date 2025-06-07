using Client.Main.Content;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(246, "Zienna, the Weapons Merchant")]
    public class Zienna : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Snow_Smith.bmd");
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
