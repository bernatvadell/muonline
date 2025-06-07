using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(371, "Leo the Helper")]
    public class Leo : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10);
            await base.Load();
            CurrentAction = (int)PlayerAction.StopMale;
        }
        protected override void HandleClick() { }
    }
}
