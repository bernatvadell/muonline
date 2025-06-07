using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(249, "Berdysh Guard")]
    public class BerdyshGuard : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10); // Plate Set
            // Weapon1.Type = (int)ModelType.Spear + 6 + MODEL_ITEM; // Berdysh
            await base.Load();
            CurrentAction = (int)PlayerAction.StopMale;
        }
        protected override void HandleClick() { }
    }
}
