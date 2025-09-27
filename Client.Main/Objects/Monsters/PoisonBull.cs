using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(8, "Poison Bull")]
    public class PoisonBull : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public PoisonBull()
        {
            RenderShadow = true;
            Scale = 1.0f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 42,
                ItemLevel = 2
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 0 -> File Number: 0 + 1 = 1
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster01.bmd");
            var item = ItemDatabase.GetItemDefinition(3, 8); // Great Scythe
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 0, 1, 2, 3, 4);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the idle sounds (index 0 or 1)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBull1.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the attack sounds (index 2 or 3)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBullAttack1.wav", Position, listenerPosition);
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBullDie.wav", Position, listenerPosition); // Death sound (index 4)
        }
    }
}