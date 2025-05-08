using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(21, "Assassin")]
    public class Assassin : MonsterObject
    {
        public Assassin()
        {
            RenderShadow = true;
            Scale = 0.95f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 14 -> File Number: 14 + 1 = 15
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster15.bmd");
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 20; (Additional info)
            // C++: b->Actions[MONSTER01_STOP2].PlaySpeed = 0.35f; (Requires mapping MonsterActionType)
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, -1, -1, 65, 66, 67);
        // No Idle sound (-1)

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mAssassinAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 65
                                                                                                                          // SoundController.Instance.PlayBufferWithAttenuation("Sound/mAssassinAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 66
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mAssassinDie.wav", Position, listenerPosition); // Index 4 -> Sound 67
        }
    }
}