using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(9, "Thunder Lich")]
    public class ThunderLich : Lich // Inherits from Lich
    {
        private WeaponObject _rightHandWeapon;
        public ThunderLich()
        {
            Scale = 1.1f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 41,
                ItemLevel = 1
            };
            Children.Add(_rightHandWeapon);
        }
        public override async Task Load()
        {
            var item = ItemDatabase.GetItemDefinition(5, 3); // Thunder Staff
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);

            await base.Load();
        }
        // Load() and sound methods inherited
        // Sounds are inherited from Lich
    }
}