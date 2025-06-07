using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(247, "Crossbow Guard")]
    public class CrossbowGuard : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10); // Plate Set
            // Weapon1.Type = (int)ModelType.Crossbow + 10 + MODEL_ITEM; // Light Crossbow
            // Weapon2.Type = MODEL_BOLT;
            await base.Load();
            CurrentAction = (int)PlayerAction.StopMale;
        }
        protected override void HandleClick() { }
    }
}
