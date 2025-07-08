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
    // Renamed to avoid conflict with Player class
    [NpcInfo(10, "Dark Knight")]
    public class DarkKnight : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public DarkKnight()
        {
            RenderShadow = true;
            Scale = 0.8f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 26
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 3 -> File Number: 3 + 1 = 4
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster04.bmd");
            var item = ItemDatabase.GetItemDefinition(0, 13); // Double Blade
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
            SetActionSpeed(MonsterActionType.Stop1, 0.25f * 1.2f);
            SetActionSpeed(MonsterActionType.Stop2, 0.20f * 1.2f);
            SetActionSpeed(MonsterActionType.Walk, 0.34f * 1.2f);
            SetActionSpeed(MonsterActionType.Attack1, 0.33f * 1.2f);
            SetActionSpeed(MonsterActionType.Attack2, 0.33f * 1.2f);
            SetActionSpeed(MonsterActionType.Shock, 0.5f * 1.2f);
            }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 15, 16, 17, 18, 19);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnight1.wav", Position, listenerPosition); // Index 0 -> Sound 15
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnightAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 17
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnightDie.wav", Position, listenerPosition); // Index 4 -> Sound 19
        }
    }
}