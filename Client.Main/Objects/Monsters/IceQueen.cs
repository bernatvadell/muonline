using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(25, "Ice Queen")]
    public class IceQueen : MonsterObject
    {
        public IceQueen()
        {
            RenderShadow = true;
            BlendMesh = 2;
            Scale = 1.1f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 18 -> File Number: 18 + 1 = 19
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster19.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 16;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 60, 61, 62, 63, 64);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceQueen1.wav", Position, listenerPosition); // Index 0 -> Sound 60
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceQueen2.wav", Position, listenerPosition); // Index 1 -> Sound 61
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceQueenAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 62
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceQueenAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 63
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceQueenDie.wav", Position, listenerPosition); // Index 4 -> Sound 64
        }
    }
}