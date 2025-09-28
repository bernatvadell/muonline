using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;
using Client.Main.Controls;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(33, "Elite Goblin")]
    public class EliteGoblin : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;

        public EliteGoblin()
        {
            RenderShadow = true;
            Scale = 1.2f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 31,
                ItemLevel = 1
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 22,
                ItemLevel = 1
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 19 -> File Number: 19 + 1 = 20 (same as Goblin)
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster20.bmd");
            var weapon = ItemDatabase.GetItemDefinition(1, 1); // Morning Star
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            var shield = ItemDatabase.GetItemDefinition(6, 1); // Horn Shield
            if (shield != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(shield.TexturePath);
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel (same as Goblin)
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    SetActionSpeed(MonsterActionType.Walk, 0.6f);
                    // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
                }
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 72, 73, 74, 75, 76);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblin1.wav", Position, listenerPosition); // Index 0 -> Sound 72
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblinAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 74
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblinDie.wav", Position, listenerPosition); // Index 4 -> Sound 76
        }
    }
}