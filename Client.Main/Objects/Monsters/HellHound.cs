using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(5, "Hell Hound")]
    public class HellHound : Hound // Inherits from Hound as it uses the same model/sounds
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;
        public HellHound()
        {
            // Override scale if needed, base constructor sets it to 0.85f
            Scale = 1.1f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 19,
                ItemLevel = 1
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 14,
                ItemLevel = 1
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            var item = ItemDatabase.GetItemDefinition(0, 7); // Falchion
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            var shield = ItemDatabase.GetItemDefinition(6, 9); // Plate Shield
            if (shield != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(shield.TexturePath);

            await base.Load();
        }

        // Load() and sound methods are inherited from Hound
        // Sounds are inherited from Hound
    }
}