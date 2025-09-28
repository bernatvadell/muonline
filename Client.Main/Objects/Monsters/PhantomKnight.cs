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
using Client.Main.Models;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(72, "Phantom Knight")]
    public class PhantomKnight : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        public PhantomKnight()
        {
            Scale = 1.45f;
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 30,
                ItemLevel = 5
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster54.bmd");
            var item = ItemDatabase.GetItemDefinition(0, 17); // Dark Breaker
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);

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

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 168, 168, 169, 169, 170);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mPhantom1.wav", Position, listenerPosition); // Sound 168
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mPhantomAttack1.wav", Position, listenerPosition); // Sound 169
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mPhantomDie.wav", Position, listenerPosition); // Sound 170
        }
    }
}
