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
    [NpcInfo(29, "Hunter")]
    public class Hunter : MonsterObject
    {
        private WeaponObject _rightHandWeapon;

        public Hunter()
        {
            RenderShadow = true;
            Scale = 0.95f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 25
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 22 -> File Number: 22 + 1 = 23
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster23.bmd");
            var weapon = ItemDatabase.GetItemDefinition(4, 10); // Arquebus
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 85, 86, 87, 88, 89)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunter1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunter2.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunterAttack1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunterAttack2.wav", Position, listenerPosition);
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunterDie.wav", Position, listenerPosition);
        }
    }
}