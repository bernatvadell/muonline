using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(30, "Forest Monster")]
    public class ForestMonster : MonsterObject
    {
        public ForestMonster()
        {
            RenderShadow = true;
            Scale = 0.75f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 23 -> File Number: 23 + 1 = 24
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster24.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 90, 91, 92, 93, 94);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWoodMon1.wav", Position, listenerPosition); // Index 0 -> Sound 90
                                                                                                                   // SoundController.Instance.PlayBufferWithAttenuation("Sound/mWoodMon2.wav", Position, listenerPosition); // Index 1 -> Sound 91
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWoodMonAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 92
                                                                                                                         // SoundController.Instance.PlayBufferWithAttenuation("Sound/mWoodMonAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 93
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWoodMonDie.wav", Position, listenerPosition); // Index 4 -> Sound 94
        }
    }
}