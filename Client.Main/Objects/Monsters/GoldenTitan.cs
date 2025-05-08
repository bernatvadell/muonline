using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(53, "Golden Titan")]
    public class GoldenTitan : MonsterObject // Uses Titan model but different sounds
    {
        public GoldenTitan()
        {
            RenderShadow = true;
            Scale = 1.8f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 39 -> File Number: 39 + 1 = 40
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster40.bmd"); // Titan's model
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 28; (Titan's bone head)
        }

        // Sounds are like Dark Knight according to C++ comment (which might be an error?)
        // Using Dark Knight sounds for now, adjust if needed based on actual game files.
        protected override void OnIdle() { base.OnIdle(); SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnight1.wav", Position, ((WalkableWorldControl)World).Walker.Position); }
        public override void OnPerformAttack(int attackType = 1) { base.OnPerformAttack(attackType); SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnightAttack1.wav", Position, ((WalkableWorldControl)World).Walker.Position); }
        public override void OnDeathAnimationStart() { base.OnDeathAnimationStart(); SoundController.Instance.PlayBufferWithAttenuation("Sound/mDarkKnightDie.wav", Position, ((WalkableWorldControl)World).Walker.Position); }
    }
}