using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(236, "Golden Archer")]
    public class GoldenArcher : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 22); // Great Dragon set (golden)
            // Weapon1.Type = (int)ModelType.Bow + 5 + MODEL_ITEM; // Silver Bow as a golden-looking bow
            // Weapon1.Level = 9;
            await base.Load();
            CurrentAction = (int)PlayerAction.StopMale;
        }
        protected override void HandleClick() { }
    }
}
