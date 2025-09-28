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
    [NpcInfo(17, "Cyclops")]
    public class Cyclops : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public Cyclops()
        {
            RenderShadow = true;
            Scale = 1.0f; // Default
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 41
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 10 -> File Number: 10 + 1 = 11
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster11.bmd");
            var item = ItemDatabase.GetItemDefinition(1, 8); // Crescent Axe
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
            SetActionSpeed(MonsterActionType.Walk, 0.28f);
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 20; (Additional info)
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 40, 41, 42, 43, 44); (Uses Ogre sounds)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the idle sounds (index 0 or 1)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgre1.wav", Position, listenerPosition); // Index 0 -> Sound 40
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgre2.wav", Position, listenerPosition); // Index 1 -> Sound 41
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the attack sounds (index 2 or 3)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgreAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 42
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgreAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 43
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgreDie.wav", Position, listenerPosition); // Index 4 -> Sound 44
        }
    }
}