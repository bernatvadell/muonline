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
    [NpcInfo(151, "Soldier")]
    public class Soldier : MonsterObject
    {
        private WeaponObject _leftHandWeapon;

        public Soldier()
        {
            Scale = 1.3f;
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 33
            };
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster41.bmd");
            var weapon = ItemDatabase.GetItemDefinition(4, 14); // Aquagold Crossbow
            if (weapon != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
        }
    }
}
