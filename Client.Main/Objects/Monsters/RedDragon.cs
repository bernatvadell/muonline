using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(42, "Red Dragon")]
    public class RedDragon : MonsterObject
    {
        public RedDragon()
        {
            RenderShadow = true;
            Scale = 1.3f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 31 -> File Number: 31 + 1 = 32
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster32.bmd");
            await base.Load();

            // Specific PlaySpeed adjustments from C++ Setting_Monster
            if (Model?.Actions != null)
            {
                // Indices might need adjustment based on actual MonsterActionType enum
                const int ATTACK1_INDEX = (int)MonsterActionType.Attack1; // Example
                const int ATTACK2_INDEX = (int)MonsterActionType.Attack2; // Example
                //const int STOP2_INDEX = ...; // Need mapping for Stop2
                //const int DIE_PLUS_1_INDEX = ...; // Need mapping for Die+1

                if (ATTACK1_INDEX < Model.Actions.Length && Model.Actions[ATTACK1_INDEX] != null)
                    Model.Actions[ATTACK1_INDEX].PlaySpeed = 0.5f;
                if (ATTACK2_INDEX < Model.Actions.Length && Model.Actions[ATTACK2_INDEX] != null)
                    Model.Actions[ATTACK2_INDEX].PlaySpeed = 0.7f;
                //if (STOP2_INDEX < Model.Actions.Length && Model.Actions[STOP2_INDEX] != null)
                //    Model.Actions[STOP2_INDEX].PlaySpeed = 0.8f;
                //if (DIE_PLUS_1_INDEX < Model.Actions.Length && Model.Actions[DIE_PLUS_1_INDEX] != null)
                //    Model.Actions[DIE_PLUS_1_INDEX].PlaySpeed = 0.8f;
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 123, 123, 124, 124, 125); (Uses Yeti/Bull sounds)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYeti1.wav", Position, listenerPosition); // Index 0 -> Sound 123
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBullAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 124
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYetiDie.wav", Position, listenerPosition); // Index 4 -> Sound 125
        }
    }
}