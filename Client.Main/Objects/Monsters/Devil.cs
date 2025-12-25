using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(37, "Devil")]
    public class Devil : MonsterObject
    {
        public Devil()
        {
            RenderShadow = true;
            Scale = 1.1f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 26 -> File Number: 26 + 1 = 27
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster27.bmd");
            await base.Load();
            // C++: Models[MODEL_MONSTER01+Type].BoneHead = 6;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 105, 105, 106, 106, 107); (Uses Yeti sounds)
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYeti1.wav", Position, listenerPosition); // Index 0 -> Sound 105
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the attack sounds (index 2 or 3)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mSatanAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 22
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mWizardAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 23

            if (attackType != 2 || World is not WalkableWorldControl world)
                return;

            ushort targetId = LastAttackTargetId;
            if (targetId == 0 || !world.TryGetWalkerById(targetId, out _))
                return;

            int boneIndex = 0;
            Vector3 localOffset = new Vector3(0f, 0f, 0f);

            Vector3 SourceProvider()
            {
                var bones = GetBoneTransforms();
                if (bones != null && boneIndex >= 0 && boneIndex < bones.Length)
                {
                    Matrix boneWorld = bones[boneIndex] * WorldPosition;
                    return Vector3.Transform(localOffset, boneWorld);
                }

                return WorldPosition.Translation;
            }

            Vector3 TargetProvider()
            {
                if (world.TryGetWalkerById(targetId, out var target))
                    return target.WorldPosition.Translation + Vector3.UnitZ * 80f;

                return WorldPosition.Translation + Vector3.UnitZ * 80f;
            }

            var effect = new ScrollOfLightningEffect(SourceProvider, TargetProvider);
            world.Objects.Add(effect);
            _ = effect.Load();
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mYetiDie.wav", Position, listenerPosition); // Index 4 -> Sound 107
        }
    }
}