using Client.Main.Content;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class PhoenixOfDarknessShield : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;

        public PhoenixOfDarknessShield()
        {
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 27
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 18
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster57.bmd");
            await base.Load();
        }
    }
}
