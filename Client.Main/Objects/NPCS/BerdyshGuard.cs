using Client.Main.Content;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Player;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(249, "Berdysh Guard")]
    public class BerdyshGuard : NPCObject
    {
        private WeaponObject _rightHandWeapon;

        public BerdyshGuard()
        {
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 42
            };
            Children.Add(_rightHandWeapon);
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10); // Plate Set
            var item = ItemDatabase.GetItemDefinition(3, 7); // Berdysh
            _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
            CurrentAction = (int)PlayerAction.PlayerStopMale;
        }
        protected override void HandleClick() { }
    }
}
