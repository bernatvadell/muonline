using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(244, "Caren the Barmaid")]
    public class Caren : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/SnowMerchant01.bmd");
            await SetBodyPartsAsync("Npc/",
                "Snow_merchant_helm", "Snow_merchant_armor", "Snow_merchant_pant", "Snow_merchant_glove", "Snow_merchant_boot",
                1);
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
