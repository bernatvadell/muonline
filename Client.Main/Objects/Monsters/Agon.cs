using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(31, "Agon")]
    public class Agon : MonsterObject
    {
        public Agon()
        {
            RenderShadow = true;
            Scale = 1.3f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 24 -> File Number: 24 + 1 = 25
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster25.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 16;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 95, 96, 97, 98, 99);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mArgon1.wav", Position, listenerPosition); // Index 0 -> Sound 95
                                                                                                                 // SoundController.Instance.PlayBufferWithAttenuation("Sound/mArgon2.wav", Position, listenerPosition); // Index 1 -> Sound 96
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mArgonAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 97
                                                                                                                       // SoundController.Instance.PlayBufferWithAttenuation("Sound/mArgonAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 98
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mArgonDie.wav", Position, listenerPosition); // Index 4 -> Sound 99
        }
    }
}