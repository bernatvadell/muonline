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
    [NpcInfo(58, "Tantalos")]
    public class Tantalos : MonsterObject
    {
        private WeaponObject _rightHandWeapon;

        public Tantalos()
        {
            Scale = 1.8f;
            BlendMesh = 2; // Normal blending, not full transparency like Zaikan
            BlendMeshLight = 1.0f;
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 43
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster43.bmd");
            var weapon = ItemDatabase.GetItemDefinition(0, 16); // Sword of Destruction
            if (weapon != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(weapon.TexturePath);
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 20;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 145, 146, 147, 148, 149);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/jaikan1.wav", Position, listenerPosition); // Sound 145
            // Consider adding logic for Sound 146 (jaikan2.wav) if desired
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            if (attackType == 1)
                SoundController.Instance.PlayBufferWithAttenuation("Sound/jaikan_attack1.wav", Position, listenerPosition); // Sound 147
            else
                SoundController.Instance.PlayBufferWithAttenuation("Sound/jaikan_attack2.wav", Position, listenerPosition); // Sound 148
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/jaikan_die.wav", Position, listenerPosition); // Sound 149
        }
    }
}
