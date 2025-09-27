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
    [NpcInfo(40, "Death Knight")]
    public class DeathKnightMonster : MonsterObject
    {
        private WeaponObject _rightHandWeapon;

        public DeathKnightMonster()
        {
            RenderShadow = true;
            Scale = 1.3f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 30
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 29 -> File Number: 29 + 1 = 30
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster30.bmd");
            // C++ shows both Giant Sword and Lighting Sword assignments, using Lighting Sword (last assignment)
            var weapon = ItemDatabase.GetItemDefinition(0, 14); // Lighting Sword
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 19;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 118, 119, 120, 121, 122);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDeathKnight1.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDeathKnightAttack1.wav", Position, listenerPosition);
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDeathKnightDie.wav", Position, listenerPosition);
        }
    }
}