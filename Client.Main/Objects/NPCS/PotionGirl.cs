using Client.Main.Content;
using Client.Main.Objects;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(253, "Potion Girl Amy")]
    public class PotionGirl : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Girl01.bmd");
            await SetBodyPartsAsync("Npc/",
                "GirlHead", "GirlUpper", "GirlLower", "Glove", "Boot",
                1);
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
