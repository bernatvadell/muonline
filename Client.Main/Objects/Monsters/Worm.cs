using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(24, "Worm")]
    public class Worm : MonsterObject
    {
        public Worm()
        {
            RenderShadow = true;
            Scale = 1.0f; // Default
        }

        public override async Task Load()
        {
            // Model Loading Type: 17 -> File Number: 17 + 1 = 18
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster18.bmd");
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
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 53, 53, 55, 55, 55);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWorm1.wav", Position, listenerPosition); // Index 0 -> Sound 53
                                                                                                                // Index 1 -> Sound 53
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWormDie.wav", Position, listenerPosition); // Index 2 -> Sound 55 (Uses Die sound)
                                                                                                                  // Index 3 -> Sound 55
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWormDie.wav", Position, listenerPosition); // Index 4 -> Sound 55
        }
    }
}