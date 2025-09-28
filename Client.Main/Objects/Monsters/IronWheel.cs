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

    [NpcInfo(57, "IronWheel")]
    public class IronWheel : MonsterObject
    {
        public IronWheel()
        {
            Scale = 1.4f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster42.bmd");
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 3;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 143, 143, 144, 144, 144);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/iron1.wav", Position, listenerPosition); // Sound 143
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/iron_attack1.wav", Position, listenerPosition); // Sound 144
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/iron_attack1.wav", Position, listenerPosition); // Sound 144
        }
    }
}
