using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(4, "Elite Bull Fighter")]
    public class EliteBullFighter : BullFighter // Inherits from BullFighter as it uses the same model/sounds
    {
        private WeaponObject _rightHandWeapon;
        public EliteBullFighter()
        {
            // Override scale if needed, base constructor sets it to 0.8f
            Scale = 1.15f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 42
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            var item = ItemDatabase.GetItemDefinition(3, 7); // Berdysh
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
        }
    }
}