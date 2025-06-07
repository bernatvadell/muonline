using Client.Main.Content;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(255, "Lumen the Barmaid")]
    public class Lumen : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Female01.bmd");
            await SetBodyPartsAsync("NPC/",
                "FemaleHead", "FemaleUpper", "FemaleLower", "FemaleGlove", "FemaleBoots",
                2);
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
