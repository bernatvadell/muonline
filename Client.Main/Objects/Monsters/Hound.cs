using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(1, "Hound")]
    public class Hound : MonsterObject
    {
        public Hound()
        {
            RenderShadow = true;
            Scale = 0.85f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 1 -> File Number: 1 + 1 = 2
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster02.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned for this monster in OpenMonsterModel's switch
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 5, 6, 7, 8, 9);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHound1.wav", Position, listenerPosition); // Index 0 -> Sound 5
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHoundAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 7
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHoundDie.wav", Position, listenerPosition); // Index 4 -> Sound 9
        }
    }
}