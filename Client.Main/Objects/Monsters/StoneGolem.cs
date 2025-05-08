using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(32, "Stone Golem")]
    public class StoneGolem : MonsterObject
    {
        public StoneGolem()
        {
            RenderShadow = true;
            Scale = 1.0f; // Default
        }

        public override async Task Load()
        {
            // Model Loading Type: 25 -> File Number: 25 + 1 = 26
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster26.bmd");
            await base.Load();
            // C++: PlaySpeed *= 0.7f for actions Stop1 to Die (except Die itself) if Type == 25
            // Apply if needed based on action indices
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 5;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 100, 101, 102, 103, 104);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGolem1.wav", Position, listenerPosition); // Index 0 -> Sound 100
                                                                                                                 // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGolem2.wav", Position, listenerPosition); // Index 1 -> Sound 101
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGolemAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 102
                                                                                                                       // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGolemAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 103
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGolemDie.wav", Position, listenerPosition); // Index 4 -> Sound 104
        }
    }
}