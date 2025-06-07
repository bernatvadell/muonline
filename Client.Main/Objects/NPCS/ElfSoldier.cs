using Client.Data;
using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Objects.Wings;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(257, "Elf Soldier")]
    public class ElfSoldier : CompositeNPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmElf", "ArmorElf", "PantElf", "GloveElf", "BootElf", 15); // Guardian Set as a placeholder
            // (Wings as WingObject).Type = (ushort)ModelType.Wings_of_Spirits;
            await base.Load();
            CurrentAction = (int)PlayerAction.StopFemale;
        }
        protected override void HandleClick() { }
    }
}
