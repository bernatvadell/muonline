using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(74, "Alpha Crust")]
    public class AlphaCrust : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;
        public AlphaCrust()
        {
            Scale = 1.3f;
            BlendMesh = 1;
            BlendMeshLight = 1.0f;

            // Enable simple color mode for cool blue-cyan tint (corresponding to BITMAP_ROBE + 5)
            EnableCustomShader = true;
            SimpleColorMode = true;
            GlowColor = new Microsoft.Xna.Framework.Vector3(1.0f, 1.4f, 1.6f); // Cool blue-cyan tint (brighter)
            GlowIntensity = 8.0f; // Test with brighter GlowColor
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 36,
                ItemLevel = 9
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 45,
                ItemLevel = 9
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster53.bmd"); // TODO
            var item = ItemDatabase.GetItemDefinition(0, 18); // Thunder Blade
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            var shield = ItemDatabase.GetItemDefinition(6, 14); // Legendary Shield
            if (shield != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(shield.TexturePath);

            await base.Load();
        }
    }
}
