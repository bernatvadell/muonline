using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(60, "BloodyWolf")]
    public class BloodyWolf : MonsterObject
    {
        public BloodyWolf()
        {
            Scale = 2.2f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster44.bmd");
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 7;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 150, 150, 151, 152, 153);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/blood1.wav", Position, listenerPosition); // Sound 150
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            if (attackType == 1)
                SoundController.Instance.PlayBufferWithAttenuation("Sound/blood_attack1.wav", Position, listenerPosition); // Sound 151
            else
                SoundController.Instance.PlayBufferWithAttenuation("Sound/blood_attack2.wav", Position, listenerPosition); // Sound 152
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/blood_die.wav", Position, listenerPosition); // Sound 153
        }
    }
}
