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
    [NpcInfo(28, "Beetle Monster")]
    public class BeetleMonster : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public BeetleMonster()
        {
            RenderShadow = true;
            Scale = 0.8f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 24
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 21 -> File Number: 21 + 1 = 22
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster22.bmd");
            var item = ItemDatabase.GetItemDefinition(3, 1); // Spear
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
            SetActionSpeed(MonsterActionType.Walk, 0.5f);
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 5;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 82, 82, 83, 83, 84);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBeetle1.wav", Position, listenerPosition);
            // Index 1 -> Sound 82
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBeetleAttack1.wav", Position, listenerPosition);
            // Index 3 -> Sound 83
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBeetleDie.wav", Position, listenerPosition); // Index 4 -> Sound 84
        }
    }
}