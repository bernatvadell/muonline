using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(48, "Lizard King")]
    public class LizardKing : MonsterObject
    {
        public LizardKing()
        {
            RenderShadow = true;
            Scale = 1.4f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 36 -> File Number: 36 + 1 = 37
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster37.bmd");
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 19;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 138, 139, 138, 139, 140)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing2.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKing2.wav", Position, listenerPosition);
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mLizardKingDie.wav", Position, listenerPosition);
        }
    }
}