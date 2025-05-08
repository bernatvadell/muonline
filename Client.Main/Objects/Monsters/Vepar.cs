using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(46, "Vepar")]
    public class Vepar : MonsterObject
    {
        public Vepar()
        {
            RenderShadow = true;
            Scale = 1.0f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 34 -> File Number: 34 + 1 = 35
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster35.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ Setting_Monster
            if (Model?.Actions != null)
            {
                const int ATTACK1_INDEX = (int)MonsterActionType.Attack1;
                const int ATTACK2_INDEX = (int)MonsterActionType.Attack2;
                if (ATTACK1_INDEX < Model.Actions.Length && Model.Actions[ATTACK1_INDEX] != null)
                    Model.Actions[ATTACK1_INDEX].PlaySpeed = 0.5f;
                if (ATTACK2_INDEX < Model.Actions.Length && Model.Actions[ATTACK2_INDEX] != null)
                    Model.Actions[ATTACK2_INDEX].PlaySpeed = 0.5f;
            }
            // C++: b->BoneHead = 20;//인어
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 132, 133, 104, 104, 133); (Uses Golem/Idle sounds)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBepar1.wav", Position, listenerPosition); // Index 0 -> Sound 132
                                                                                                                 // SoundController.Instance.PlayBufferWithAttenuation("Sound/mBepar2.wav", Position, listenerPosition); // Index 1 -> Sound 133
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGolemAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 104 (Uses Golem)
                                                                                                                       // Index 3 -> Sound 104
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBepar2.wav", Position, listenerPosition); // Index 4 -> Sound 133
        }
    }
}