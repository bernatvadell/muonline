using Client.Main.Content;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(59, "Zaikan")]
    public class Zaikan : MonsterObject
    {
        private WeaponObject _rightHandWeapon;

        public Zaikan()
        {
            Scale = 2.1f;
            BlendMesh = -2; // Makes the entire monster semi-transparent like in original
            BlendMeshLight = 1.0f;
            Type = 1; // SubType equivalent
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 43
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster43.bmd");
            var weapon = ItemDatabase.GetItemDefinition(5, 8); // Staff of Destruction
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
            //TODO Zaikan uses tantalos model with some different blending options
        }
    }
}
