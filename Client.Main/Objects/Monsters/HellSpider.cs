using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(13, "Hell Spider")]
    public class HellSpider : MonsterObject
    {
        public HellSpider()
        {
            RenderShadow = true;
            Scale = 1.1f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 8 -> File Number: 8 + 1 = 9
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster09.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 0.7f;
                }
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 32, 33, 33, 33, 34);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHellSpider1.wav", Position, listenerPosition); // Index 0 -> Sound 32
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHellSpiderAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 33
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mHellSpiderDie.wav", Position, listenerPosition); // Index 4 -> Sound 34
        }
    }
}