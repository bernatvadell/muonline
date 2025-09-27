using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(33, "Elite Goblin")]
    public class EliteGoblin : Goblin // Inherits from Goblin as it uses the same model/sounds
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;

        public EliteGoblin()
        {
            Scale = 1.2f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 31,
                ItemLevel = 1
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 22,
                ItemLevel = 1
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            // Uses the same model as Goblin
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster20.bmd");
            var weapon = ItemDatabase.GetItemDefinition(1, 1); // Morning Star
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            var shield = ItemDatabase.GetItemDefinition(6, 1); // Horn Shield
            if (shield != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(shield.TexturePath);
            await base.Load();
            // Inherits sounds and playspeed adjustments from Goblin
        }
    }
}