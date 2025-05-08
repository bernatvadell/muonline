using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(26, "Goblin")]
    public class Goblin : MonsterObject
    {
        public Goblin()
        {
            RenderShadow = true;
            Scale = 0.8f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 19 -> File Number: 19 + 1 = 20
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster20.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 0.6f;
                }
            }
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 72, 73, 74, 75, 76);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblin1.wav", Position, listenerPosition); // Index 0 -> Sound 72
                                                                                                                  // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblin2.wav", Position, listenerPosition); // Index 1 -> Sound 73
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblinAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 74
                                                                                                                        // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblinAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 75
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGoblinDie.wav", Position, listenerPosition); // Index 4 -> Sound 76
        }
    }
}