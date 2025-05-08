using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(29, "Hunter")]
    public class Hunter : MonsterObject
    {
        public Hunter()
        {
            RenderShadow = true;
            Scale = 0.95f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 22 -> File Number: 22 + 1 = 23
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster23.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 85, 86, 87, 88, 89);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunter1.wav", Position, listenerPosition); // Index 0 -> Sound 85
                                                                                                                  // SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunter2.wav", Position, listenerPosition); // Index 1 -> Sound 86
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunterAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 87
                                                                                                                        // SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunterAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 88
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHunterDie.wav", Position, listenerPosition); // Index 4 -> Sound 89
        }
    }
}