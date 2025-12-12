using System;
using System.Threading.Tasks;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Red mist emitter that follows a player's hand bone.
    /// Used to visualize the Elf Soldier NPC buff.
    /// </summary>
    public class ElfBuffMistEmitter : WaterMistParticleSystem
    {
        private readonly PlayerObject _owner;
        private readonly int _boneIndex;
        private readonly Vector3 _offset;
        private readonly float _baseEmissionRate;
        private readonly DynamicLight _dynamicLight;
        private float _pulseTimer;
        private float _spawnBoostTimer = 0.35f;
        private const float SpawnBoostRate = 32f;

        public ElfBuffMistEmitter(PlayerObject owner, int boneIndex, Vector3 offset)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _boneIndex = boneIndex;
            _offset = offset;

            EmissionRate = 30f;
            SpawnRadius = new Vector2(4f, 4f);
            LifetimeRange = new Vector2(0.3f, 0.4f);
            ScaleRange = new Vector2(0.65f, 1.25f);
            UseDistanceEmissionScaling = false;
            UseDistanceScaling = false;
            UseConstantWorldSize = true;
            ReferenceDistance = 800f;
            HorizontalVelocityRange = new Vector2(-8f, 8f);
            UpwardVelocityRange = new Vector2(22f, 30f);
            UpwardAcceleration = 10f;
            ScaleGrowth = 0.45f;
            MaxDistance = 3200f;
            Wind = new Vector2(4f, 4f);
            ParticleColor = new Color(240, 60, 60, 210);
            IsTransparent = true;
            AffectedByTransparency = true;

            _baseEmissionRate = EmissionRate;

            // Create red dynamic light for the hand effect
            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.2f, 0.15f),
                Radius = 120f,
                Intensity = 0.6f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            if (World?.Terrain != null)
            {
                _dynamicLight.Position = Position;
                World.Terrain.AddDynamicLight(_dynamicLight);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!TryUpdateTransform())
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_spawnBoostTimer > 0f)
                _spawnBoostTimer -= dt;

            UpdateEmissionState();
            UpdateColorPulse(dt);

            base.Update(gameTime);
        }

        private bool TryUpdateTransform()
        {
            if (_owner == null || _owner.Status == GameControlStatus.Disposed || _owner.World == null)
            {
                RemoveSelf();
                return false;
            }

            if (_owner.Status != GameControlStatus.Ready)
                return true;

            if (_owner.TryGetBoneWorldMatrix(_boneIndex, out var worldMatrix))
            {
                Position = worldMatrix.Translation + _offset;
            }

            return true;
        }

        private void UpdateEmissionState()
        {
            bool shouldHide = _owner.Status != GameControlStatus.Ready || _owner.Hidden || _owner.IsDead;

            Hidden = shouldHide;
            UpdateDynamicLightVisibility(shouldHide);

            if (shouldHide)
            {
                EmissionRate = 0f;
                return;
            }

            float rate = _baseEmissionRate;
            if (_spawnBoostTimer > 0f)
            {
                rate = MathF.Max(rate, SpawnBoostRate);
            }

            EmissionRate = rate;
        }

        private void UpdateColorPulse(float dt)
        {
            _pulseTimer += dt;

            float pulse = MathF.Sin(_pulseTimer * 6f) * 0.25f + 0.75f;
            byte r = (byte)Math.Clamp(220f + 40f * pulse, 0f, 255f);
            byte g = (byte)Math.Clamp(35f + 45f * pulse, 0f, 255f);
            byte b = (byte)Math.Clamp(35f + 45f * pulse, 0f, 255f);
            byte a = (byte)Math.Clamp(170f + 70f * pulse, 0f, 255f);

            ParticleColor = new Color(r, g, b, a);

            // Update dynamic light to pulse with the color
            if (_dynamicLight != null)
            {
                _dynamicLight.Position = Position;
                _dynamicLight.Intensity = 0.4f + pulse * 0.4f;
                _dynamicLight.Color = new Vector3(
                    0.9f + pulse * 0.1f,
                    0.15f + pulse * 0.1f,
                    0.1f + pulse * 0.1f);
            }
        }

        private void UpdateDynamicLightVisibility(bool shouldHide)
        {
            if (_dynamicLight != null)
            {
                _dynamicLight.Intensity = shouldHide ? 0f : 0.6f;
            }
        }

        public override float Depth => Position.Y + Position.Z;

        private void RemoveSelf()
        {
            if (Parent != null)
            {
                Parent.Children.Remove(this);
                return;
            }

            if (World != null)
            {
                World.Objects.Remove(this);
                return;
            }

            Dispose();
        }

        public override void Dispose()
        {
            if (World?.Terrain != null && _dynamicLight != null)
            {
                World.Terrain.RemoveDynamicLight(_dynamicLight);
            }

            base.Dispose();
        }
    }
}
