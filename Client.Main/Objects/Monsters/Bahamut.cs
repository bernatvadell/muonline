using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(45, "Bahamut")]
    public class Bahamut : MonsterObject
    {
        public Bahamut()
        {
            RenderShadow = true;
            Scale = 0.6f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 33 -> File Number: 33 + 1 = 34
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster34.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 130, 130, 131, 131, 130); (Uses Yeti sound)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBahamut1.wav", Position, listenerPosition); // Index 0 -> Sound 130
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYeti1.wav", Position, listenerPosition); // Index 2 -> Sound 131
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBahamut1.wav", Position, listenerPosition); // Index 4 -> Sound 130
        }
    }
}