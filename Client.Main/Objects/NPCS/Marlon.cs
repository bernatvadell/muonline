using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(229, "Marlon")]
    public class Marlon : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10);
            
            Helm.ItemLevel = 11;
            Armor.ItemLevel = 11;
            Pants.ItemLevel = 11;
            Gloves.ItemLevel = 11;
            Boots.ItemLevel = 11;
            
            // Weapon1.Type = (int)ModelType.Spear + 6 + MODEL_ITEM; // Berdysh
            await base.Load();
            CurrentAction = (int)PlayerAction.PlayerStopMale;
        }
        protected override void HandleClick() { }
    }
}
