using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    // Renamed to avoid conflict with Player class
    [NpcInfo(10, "Dark Knight")]
    public class DarkKnight : MonsterObject
    {
        public DarkKnight()
        {
            RenderShadow = true;
            Scale = 0.8f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 3 -> File Number: 3 + 1 = 4
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster04.bmd");
            await base.Load();
            // C++: PlaySpeed *= 1.2f for actions Stop1 to Die (except Die itself) if Type == 3
            if (Model?.Actions != null)
            {
                // Apply speed adjustment to relevant actions if needed
                // Example for Walk:
                // const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                // if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                // {
                //     Model.Actions[MONSTER_ACTION_WALK].PlaySpeed *= 1.2f;
                // }
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 15, 16, 17, 18, 19);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnight1.wav", Position, listenerPosition); // Index 0 -> Sound 15
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnightAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 17
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnightDie.wav", Position, listenerPosition); // Index 4 -> Sound 19
        }
    }
}