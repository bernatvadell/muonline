using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(23, "Hommerd")]
    public class Hommerd : MonsterObject
    {
        public Hommerd()
        {
            RenderShadow = true;
            Scale = 1.15f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 16 -> File Number: 16 + 1 = 17
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster17.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 56, 57, 58, 58, 59);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHomord1.wav", Position, listenerPosition); // Index 0 -> Sound 56
                                                                                                                  // SoundController.Instance.PlayBufferWithAttenuation("Sound/mHomord2.wav", Position, listenerPosition); // Index 1 -> Sound 57
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHomordAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 58
                                                                                                                        // Index 3 -> Sound 58
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHomordDie.wav", Position, listenerPosition); // Index 4 -> Sound 59
        }
    }
}