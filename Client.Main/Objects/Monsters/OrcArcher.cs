using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(64, "Orc Archer")]
    public class OrcArcher : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public OrcArcher()
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
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster47.bmd");
            var item = ItemDatabase.GetItemDefinition(4, 3); // Battle Bow
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);

            await base.Load();
        }
    }
}
