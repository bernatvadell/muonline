using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(6, "Lich")]
    public class Lich : MonsterObject
    {
        public Lich()
        {
            RenderShadow = true;
            Scale = 0.85f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 4 -> File Number: 4 + 1 = 5
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster05.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned for this monster in OpenMonsterModel's switch
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 20, 21, 22, 23, 24);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWizard1.wav", Position, listenerPosition); // Index 0 -> Sound 20
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWizardAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 22
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mWizardDie.wav", Position, listenerPosition); // Index 4 -> Sound 24
        }
    }
}