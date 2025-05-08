using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(28, "Beetle Monster")]
    public class BeetleMonster : MonsterObject
    {
        public BeetleMonster()
        {
            RenderShadow = true;
            Scale = 0.8f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 21 -> File Number: 21 + 1 = 22
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster22.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 0.5f;
                }
            }
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 5;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 82, 82, 83, 83, 84);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBeetle1.wav", Position, listenerPosition); // Index 0 -> Sound 82
                                                                                                                  // Index 1 -> Sound 82
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBeetleAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 83
                                                                                                                        // Index 3 -> Sound 83
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBeetleDie.wav", Position, listenerPosition); // Index 4 -> Sound 84
        }
    }
}