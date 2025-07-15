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
            Helm.ItemLevel = 7;
            Armor.ItemLevel = 7;
            Pants.ItemLevel = 7;
            Gloves.ItemLevel = 7;
            Boots.ItemLevel = 7;

            // Set item properties for testing
            Armor.IsAncientItem = true;
            Helm.IsAncientItem = true;
            Pants.IsAncientItem = true;
            Gloves.IsAncientItem = true;
            Boots.IsAncientItem = true;

            await base.Load();
            CurrentAction = (int)PlayerAction.PlayerStopMale;
        }
        protected override void HandleClick() { }
    }
}
