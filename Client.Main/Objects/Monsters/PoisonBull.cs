using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(8, "Poison Bull")]
    public class PoisonBull : BullFighter // Inherits from BullFighter
    {
        private WeaponObject _rightHandWeapon;
        public PoisonBull()
        {
            Scale = 1.0f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 42
            };
            Children.Add(_rightHandWeapon);
        }
        public override async Task Load()
        {
            var item = ItemDatabase.GetItemDefinition(3, 8); // Great Scythe
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);

            await base.Load();
        }
        // Load() and sound methods inherited
        // Sounds are inherited from BullFighter
    }
}