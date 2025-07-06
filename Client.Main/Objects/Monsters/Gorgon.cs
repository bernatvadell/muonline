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
    [NpcInfo(18, "Gorgon")]
    public class Gorgon : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public Gorgon()
        {
            RenderShadow = true;
            Scale = 1.5f; // Set according to C++ Setting_Monster
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 30
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 11 -> File Number: 11 + 1 = 12
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster12.bmd");
            var item = ItemDatabase.GetItemDefinition(5, 4); // Gorgon Staff
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 45, 46, 47, 48, 49);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGorgon1.wav", Position, listenerPosition); // Index 0 -> Sound 45
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGorgonAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 47
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGorgonDie.wav", Position, listenerPosition); // Index 4 -> Sound 49
        }
    }
}