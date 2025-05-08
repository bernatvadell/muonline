using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(27, "Chain Scorpion")]
    public class ChainScorpion : MonsterObject
    {
        public ChainScorpion()
        {
            RenderShadow = true;
            Scale = 1.1f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 20 -> File Number: 20 + 1 = 21
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster21.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 0.4f;
                }
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 77, 78, 79, 80, 81);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mScorpion1.wav", Position, listenerPosition); // Index 0 -> Sound 77
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mScorpion2.wav", Position, listenerPosition); // Index 1 -> Sound 78
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mScorpionAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 79
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mScorpionAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 80
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mScorpionDie.wav", Position, listenerPosition); // Index 4 -> Sound 81
        }
    }
}