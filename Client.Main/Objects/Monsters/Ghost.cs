using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; // Needed for BlendState
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(11, "Ghost")]
    public class Ghost : MonsterObject // Renamed
    {
        public Ghost()
        {
            RenderShadow = false; // Ghosts typically don't cast shadows
            Scale = 1.0f; // Default
            Alpha = 0.6f; // Semi-transparent
            BlendState = Blendings.Alpha; // Use Alpha blending
        }

        public override async Task Load()
        {
            // Model Loading Type: 7 -> File Number: 7 + 1 = 8
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster08.bmd");
            await base.Load();
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 35, 36, 37, 38, 39);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGhost1.wav", Position, listenerPosition); // Index 0 -> Sound 35
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGhostAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 37
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGhostDie.wav", Position, listenerPosition); // Index 4 -> Sound 39
        }
    }
}