using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(38, "Balrog")]
    public class Balrog : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public Balrog()
        {
            RenderShadow = true;
            Scale = 1.6f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 17
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 27 -> File Number: 27 + 1 = 28
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster28.bmd");
            var item = ItemDatabase.GetItemDefinition(3, 9); // Bill of Balrog
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
            // C++: Models[MODEL_MONSTER01+Type].StreamMesh = 1; // May need special handling for streaming meshes if applicable
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 108, 109, 110, 111, 112);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBalrog1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mBalrog2.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWizardAttack2.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGorgonAttack2.wav", Position, listenerPosition);
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBalrogDie.wav", Position, listenerPosition); // Index 4 -> Sound 112
        }
    }
}