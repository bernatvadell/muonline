using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(22, "Ice Monster")]
    public class IceMonster : MonsterObject
    {
        public IceMonster()
        {
            RenderShadow = true;
            Scale = 1.0f; // Default
            BlendMesh = -2; // Use full blending like other semi-transparent monsters
            BlendMeshLight = 0.7f; // Reduced light for more subtle blending
            Alpha = 0.85f; // Slightly transparent for better blending with environment
        }

        public override async Task Load()
        {
            // Model Loading Type: 15 -> File Number: 15 + 1 = 16
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster16.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 19; (Additional info)
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 50, 51, 50, 50, 52)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceMonster1.wav", Position, listenerPosition);
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceMonster2.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceMonster1.wav", Position, listenerPosition);
            // Index 3 -> Sound 50
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mIceMonsterDie.wav", Position, listenerPosition);
        }
    }
}