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
    [NpcInfo(48, "Lizard King")]
    public class LizardKing : MonsterObject
    {
        private WeaponObject _rightHandWeapon;

        public LizardKing()
        {
            RenderShadow = true;
            Scale = 1.4f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 52
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 36 -> File Number: 36 + 1 = 37
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster37.bmd");
            var weapon = ItemDatabase.GetItemDefinition(5, 11); // Staff of Resurrection
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 19;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 138, 139, 138, 139, 140)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing2.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing2.wav", Position, listenerPosition);
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKingDie.wav", Position, listenerPosition);
        }
    }
}