using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(151, "Soldier")]
    public class Soldier : MonsterObject
    {
        private WeaponObject _leftHandWeapon;

        public Soldier()
        {
            Scale = 1.3f;
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 33
            };
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster41.bmd");
            var weapon = ItemDatabase.GetItemDefinition(4, 14); // Aquagold Crossbow
            if (weapon != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6; (for Soldier)
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 138, 139, 138, 139, 140) - same as Lizard
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing1.wav", Position, listenerPosition); // Sound 138
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            if (attackType == 1)
                SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing1.wav", Position, listenerPosition); // Sound 138
            else
                SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing2.wav", Position, listenerPosition); // Sound 139
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGorgonDie.wav", Position, listenerPosition); // Sound 140
        }
    }
}
