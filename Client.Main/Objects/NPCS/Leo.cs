using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(371, "Leo the Helper")]
    public class Leo : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10);

            // Set item levels +7 or higher to see effects
            Helm.ItemLevel = 11;
            Armor.ItemLevel = 11;
            Pants.ItemLevel = 11;
            Gloves.ItemLevel = 11;
            Boots.ItemLevel = 11;

            // Set item properties for testing
            Armor.IsExcellentItem = true;
            Helm.IsAncientItem = true;

            await base.Load();
            CurrentAction = (int)PlayerAction.PlayerStopMale;
        }
        protected override void HandleClick() { }
    }
}
