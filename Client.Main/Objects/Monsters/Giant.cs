using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(7, "Giant")]
    public class Giant : MonsterObject
    {
        public Giant()
        {
            RenderShadow = true;
            Scale = 1.6f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 5 -> File Number: 5 + 1 = 6
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster06.bmd");
            await base.Load();
            // C++: PlaySpeed *= 0.7f for actions Stop1 to Die (except Die itself)
            // This needs adjustment based on actual action indices if available
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 25, 26, 27, 28, 29);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiant1.wav", Position, listenerPosition); // Index 0 -> Sound 25
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 27
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantDie.wav", Position, listenerPosition); // Index 4 -> Sound 29
        }
    }
}