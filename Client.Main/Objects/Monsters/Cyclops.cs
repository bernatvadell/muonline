using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(17, "Cyclops")]
    public class Cyclops : MonsterObject
    {
        public Cyclops()
        {
            RenderShadow = true;
            Scale = 1.0f; // Default
        }

        public override async Task Load()
        {
            // Model Loading Type: 10 -> File Number: 10 + 1 = 11
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster11.bmd");
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
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 20; (Additional info)
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 40, 41, 42, 43, 44); (Uses Ogre sounds)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgre1.wav", Position, listenerPosition); // Index 0 -> Sound 40
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgreAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 42
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mOgreDie.wav", Position, listenerPosition); // Index 4 -> Sound 44
        }
    }
}