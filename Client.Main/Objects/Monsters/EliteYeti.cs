using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(20, "Elite Yeti")]
    public class EliteYeti : MonsterObject // Note: Sounds differ slightly from Yeti in C++
    {
        public EliteYeti()
        {
            RenderShadow = true;
            Scale = 1.4f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 13 -> File Number: 13 + 1 = 14
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster14.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 0.28f;
                }
            }
            // C++: Models[MODEL_ELITE_YETI].BoneHead = 20; (Additional info)
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_ELITE_YETI, 68, 69, 70, 70, 71);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the idle sounds (index 0 or 1 -> sound 68 or 69)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYeti1.wav", Position, listenerPosition); // Sound 68
            // Consider adding logic for Sound 69 (mYeti2.wav) if desired
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYetiAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 70
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYetiDie.wav", Position, listenerPosition); // Index 4 -> Sound 71
        }
    }
}