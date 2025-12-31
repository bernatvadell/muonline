#nullable enable
using System;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Death Stab victim impact effect - creates lightning bolts between random bones
    /// on the victim's body for 35 frames after being struck.
    /// </summary>
    public sealed class DeathStabVictimEffect : EffectObject
    {
        private const float VictimEffectLifeFrames = 35f;
        private const float LightningScale = 20f;
        private const int MaxActiveLightningBolts = 12;

        private readonly WalkerObject _victim;
        private float _lifeFrames;

        public DeathStabVictimEffect(WalkerObject victim)
        {
            _victim = victim ?? throw new ArgumentNullException(nameof(victim));
            _lifeFrames = VictimEffectLifeFrames;

            IsTransparent = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-100f, -100f, -100f),
                new Vector3(100f, 100f, 200f));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
                _ = Load();

            if (Status != GameControlStatus.Ready)
                return;

            if (_victim.Status == GameControlStatus.Disposed || _victim.World == null)
            {
                RemoveSelf();
                return;
            }

            Position = _victim.Position;

            if (FPSCounter.Instance.RandFPSCheck(2))
            {
                SpawnLightningBetweenBones();
            }

            float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            _lifeFrames -= factor;

            if (_lifeFrames <= 0f)
            {
                RemoveSelf();
            }
        }

        private void SpawnLightningBetweenBones()
        {
            if (World == null || _victim.Model?.Bones == null)
                return;

            var boneTransforms = _victim.GetBoneTransforms();
            if (boneTransforms == null || boneTransforms.Length < 2)
                return;

            int boneCount = boneTransforms.Length;
            int bone1Index = MuGame.Random.Next(0, boneCount);
            int bone2Index = MuGame.Random.Next(0, boneCount);

            if (bone1Index == bone2Index && boneCount > 1)
            {
                bone2Index = (bone2Index + 1) % boneCount;
            }

            Vector3 bone1Pos = boneTransforms[bone1Index].Translation;
            Vector3 bone2Pos = boneTransforms[bone2Index].Translation;

            Vector3 midPoint = (bone1Pos + bone2Pos) * 0.5f;
            float distance = Vector3.Distance(bone1Pos, bone2Pos);

            Vector3 direction = bone2Pos - bone1Pos;
            if (direction.LengthSquared() > 0.001f)
            {
                direction.Normalize();
            }
            else
            {
                direction = Vector3.UnitZ;
            }

            float angleZ = MathF.Atan2(direction.Y, direction.X);

            var lightning = new JointThunderEffect
            {
                Position = midPoint,
                Angle = new Vector3(0f, 0f, angleZ),
                Scale = MathF.Max(distance / 50f, 0.5f),
                Light = Vector3.One
            };

            if (_victim.Parent != null)
                _victim.Parent.Children.Add(lightning);
            else
                World.Objects.Add(lightning);

            _ = lightning.Load();

            DestroyOldLightningBolts();
        }

        private void DestroyOldLightningBolts()
        {
            if (World == null)
                return;

            int lightningCount = 0;
            for (int i = World.Objects.Count - 1; i >= 0; i--)
            {
                if (World.Objects[i] is JointThunderEffect)
                {
                    lightningCount++;
                    if (lightningCount > MaxActiveLightningBolts)
                    {
                        World.Objects[i].Dispose();
                        World.Objects.RemoveAt(i);
                    }
                }
            }
        }

        private void RemoveSelf()
        {
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }
    }
}
