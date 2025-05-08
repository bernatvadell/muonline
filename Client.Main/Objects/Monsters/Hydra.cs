using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(49, "Hydra")]
    public class Hydra : MonsterObject
    {
        public Hydra()
        {
            RenderShadow = true;
            Scale = 1.0f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 37 -> File Number: 37 + 1 = 38
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster38.bmd");
            await base.Load();

            // Specific PlaySpeed adjustments from C++ Setting_Monster
            if (Model?.Actions != null)
            {
                // C++: PlaySpeed *= 0.4f for actions Stop1 to Die (except Die itself) if Type == 37
                // Apply if needed based on action indices

                const int ATTACK1_INDEX = (int)MonsterActionType.Attack1;
                const int ATTACK2_INDEX = (int)MonsterActionType.Attack2;
                const int DIE_INDEX = (int)MonsterActionType.Die;

                if (ATTACK1_INDEX < Model.Actions.Length && Model.Actions[ATTACK1_INDEX] != null)
                    Model.Actions[ATTACK1_INDEX].PlaySpeed = 0.15f;
                if (ATTACK2_INDEX < Model.Actions.Length && Model.Actions[ATTACK2_INDEX] != null)
                    Model.Actions[ATTACK2_INDEX].PlaySpeed = 0.15f;
                if (DIE_INDEX < Model.Actions.Length && Model.Actions[DIE_INDEX] != null)
                    Model.Actions[DIE_INDEX].PlaySpeed = 0.2f;
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 141, 141, 142, 142, 141);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHydra1.wav", Position, listenerPosition); // Index 0 -> Sound 141
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHydraAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 142
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHydra1.wav", Position, listenerPosition); // Index 4 -> Sound 141
        }
    }
}