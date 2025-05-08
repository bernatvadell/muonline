using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; // Needed for BlendState
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(36, "Shadow")]
    public class ShadowMonster : MonsterObject // Renamed
    {
        public ShadowMonster()
        {
            RenderShadow = false;
            Scale = 1.2f; // Set according to C++ Setting_Monster
            Alpha = 0.7f; // Default transparency
            BlendState = Blendings.Alpha;
        }

        public override async Task Load()
        {
            // Model Loading Type: 28 -> File Number: 28 + 1 = 29
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster29.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 0.3f;
                }
            }
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 5;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 113, 114, 115, 116, 117);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mShadow1.wav", Position, listenerPosition); // Index 0 -> Sound 113
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mShadowAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 115
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mShadowDie.wav", Position, listenerPosition); // Index 4 -> Sound 117
        }
    }
}