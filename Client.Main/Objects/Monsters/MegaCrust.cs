using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Client.Main.Models;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(71, "Mega Crust")]
    public class MegaCrust : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;
        public MegaCrust()
        {
            Scale = 1.1f;
            BlendMesh = 1;
            BlendMeshLight = 1.0f;
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 36,
                ItemLevel = 5
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 45,
                ItemLevel = 0
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster53.bmd");
            var item = ItemDatabase.GetItemDefinition(0, 18); // Thunder Blade
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            var shield = ItemDatabase.GetItemDefinition(6, 14); // Legendary Shield
            if (shield != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(shield.TexturePath);

            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_DIE = (int)MonsterActionType.Die;
                if (MONSTER_ACTION_DIE < Model.Actions.Length && Model.Actions[MONSTER_ACTION_DIE] != null)
                {
                    Model.Actions[MONSTER_ACTION_DIE].PlaySpeed = 0.22f;
                }
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 180, 180, 181, 181, 182);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mMegaCrust1.wav", Position, listenerPosition); // Sound 180
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mMegaCrustAttack1.wav", Position, listenerPosition); // Sound 181
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mMegaCrustDie.wav", Position, listenerPosition); // Sound 182
        }
    }
}
