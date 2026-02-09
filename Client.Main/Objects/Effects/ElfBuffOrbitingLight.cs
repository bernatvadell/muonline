using System;
using System.Threading.Tasks;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controls;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Glowing magical orb that orbits the player with a smooth round trail and sparkle effects.
    /// The orb stays rigidly attached to the player's position.
    /// </summary>
    public class ElfBuffOrbitingLight : SpriteObject
    {
        private readonly PlayerObject _owner;
        private readonly ElfBuffOrbTrail _trail;
        private readonly float _baseRadius;
        private readonly float _heightOffset;
        private readonly float _orbitSpeed;
        private readonly float _verticalBobSpeed;
        private readonly float _verticalBobAmount;
        private readonly float _phaseOffset;
        private readonly float _trailHue;
        private readonly bool _reverseOrbit;
        private readonly float _pulsePhase;
        private readonly float _snakeYawSway;
        private readonly float _snakeYawFrequency;
        private readonly float _snakePitchAmplitude;
        private readonly float _snakePitchSpeed;
        private readonly float _radiusWobble;
        private readonly float _snakePhaseOffset;
        private readonly float _lightBaseRadius;
        private readonly float _lightRadiusJitter;
        private readonly DynamicLight _dynamicLight;
        private bool _lightAdded;

        private float _orbitAngle;
        private float _time;
        private float _sparkleTimer;
        private float _currentLightIntensity = 1f;

        // Current orbital offset from owner (computed each frame)
        private Vector3 _orbitalOffset;

        // Visual parameters
        private const float PulseSpeed = 4.5f;
        private const float PulseMin = 0.7f;
        private const float PulseMax = 1.15f;
        private const float BaseOrbScale = 1.2f;
        private const float SparkleInterval = 0.12f;
        private const int MaxSparklesPerFrame = 12;
        private static int _sparkleFrame = -1;
        private static int _sparklesThisFrame = 0;

        public override string TexturePath => "Effect/Shiny02.jpg";

        public ElfBuffOrbitingLight(PlayerObject owner, float radius, float heightOffset, int orbitIndex = 0, int totalOrbits = 1)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _baseRadius = MathF.Max(35f, radius);
            _heightOffset = heightOffset;
            int totalOrbs = Math.Max(1, totalOrbits);

            // Visual setup
            // Render in the same world pass as walkers so front/back relation with the owner is depth-correct.
            IsTransparent = false;
            AffectedByTransparency = false;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            BoundingBoxLocal = new BoundingBox(new Vector3(-20f, -20f, -20f), new Vector3(20f, 20f, 20f));
            LightEnabled = true;
            Alpha = 1f;
            Scale = BaseOrbScale;

            // Distribute orbits evenly around the player
            _phaseOffset = MathHelper.TwoPi / totalOrbs * orbitIndex;
            _orbitAngle = _phaseOffset;

            // Randomize motion parameters for organic feel
            _orbitSpeed = MathHelper.Lerp(1.4f, 2.0f, (float)MuGame.Random.NextDouble());
            _verticalBobSpeed = MathHelper.Lerp(2.0f, 3.0f, (float)MuGame.Random.NextDouble());
            _verticalBobAmount = MathHelper.Lerp(25f, 45f, (float)MuGame.Random.NextDouble());
            _pulsePhase = MathHelper.TwoPi * (float)MuGame.Random.NextDouble();
            _reverseOrbit = orbitIndex % 2 == 1;
            _snakeYawSway = MathHelper.Lerp(0.25f, 0.55f, (float)MuGame.Random.NextDouble());
            _snakeYawFrequency = MathHelper.Lerp(0.8f, 1.6f, (float)MuGame.Random.NextDouble());
            _snakePitchAmplitude = MathHelper.Lerp(0.2f, 0.5f, (float)MuGame.Random.NextDouble());
            _snakePitchSpeed = MathHelper.Lerp(1.2f, 1.8f, (float)MuGame.Random.NextDouble());
            _radiusWobble = MathHelper.Lerp(8f, 14f, (float)MuGame.Random.NextDouble());
            _snakePhaseOffset = MathHelper.TwoPi * (float)MuGame.Random.NextDouble();
            _lightBaseRadius = MathHelper.Lerp(120f, 180f, (float)MuGame.Random.NextDouble());
            _lightRadiusJitter = MathHelper.Lerp(8f, 18f, (float)MuGame.Random.NextDouble());

            // Color variation - green to cyan range
            _trailHue = MathHelper.Lerp(0.85f, 1.15f, (float)MuGame.Random.NextDouble());

            // Create smooth round trail with circular sprites
            Color trailColor = new Color(
                (int)(100 * _trailHue),
                (int)(255 * MathHelper.Clamp(_trailHue, 0.9f, 1.1f)),
                (int)(180 * _trailHue));

            _trail = new ElfBuffOrbTrail(trailColor, 1.2f);
            _trail.SamplePoint = GetCurrentOrbPosition;
            _trail.ReferencePoint = GetOwnerPosition;

            float lightGreen = MathHelper.Clamp(0.85f * _trailHue, 0.6f, 1.1f);
            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(0.25f * _trailHue, lightGreen, 0.5f * _trailHue),
                Radius = _lightBaseRadius,
                Intensity = 1f
            };
        }

        /// <summary>
        /// Gets the current world position of the owner/player.
        /// </summary>
        private Vector3 GetOwnerPosition()
        {
            if (_owner == null || _owner.Status == GameControlStatus.Disposed)
                return Vector3.Zero;

            return _owner.WorldPosition.Translation;
        }

        /// <summary>
        /// Gets the current world position of the orb based on owner's position + orbital offset.
        /// </summary>
        private Vector3 GetCurrentOrbPosition()
        {
            if (_owner == null || _owner.Status == GameControlStatus.Disposed)
                return Position;

            return _owner.WorldPosition.Translation + _orbitalOffset;
        }

        public override async Task Load()
        {
            await base.Load();
            Children.Add(_trail);
            await _trail.Load();

            if (World?.Terrain != null)
            {
                _dynamicLight.Position = GetCurrentOrbPosition();
                World.Terrain.AddDynamicLight(_dynamicLight);
                _lightAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!TrySyncOwner())
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;

            // Update orbit angle
            float direction = _reverseOrbit ? -1f : 1f;
            _orbitAngle += _orbitSpeed * direction * dt;

            // Calculate orbital offset with serpentine movement across a sphere
            float dynamicRadius = _baseRadius + MathF.Sin(_time * 1.6f + _phaseOffset) * _radiusWobble;
            float yawSway = MathF.Sin(_time * _snakeYawFrequency + _phaseOffset) * _snakeYawSway;
            float pitch = MathF.Sin(_time * _snakePitchSpeed + _snakePhaseOffset) * _snakePitchAmplitude;
            float yaw = _orbitAngle + yawSway;
            float verticalBob = MathF.Sin(_time * _verticalBobSpeed + _phaseOffset) * (_verticalBobAmount * 0.35f);

            Vector3 directionVector = new Vector3(
                MathF.Cos(yaw) * MathF.Cos(pitch),
                MathF.Sin(yaw) * MathF.Cos(pitch),
                MathF.Sin(pitch));

            // Store orbital offset for use in GetCurrentOrbPosition and drawing
            _orbitalOffset = directionVector * dynamicRadius + new Vector3(0f, 0f, _heightOffset + verticalBob);

            // Set position to current world position (owner + offset)
            Position = _owner.WorldPosition.Translation + _orbitalOffset;

            // Pulsing glow effect
            float pulse = MathHelper.Lerp(PulseMin, PulseMax,
                0.5f + 0.5f * MathF.Sin(_time * PulseSpeed + _pulsePhase));
            Alpha = pulse;
            Scale = BaseOrbScale * MathHelper.Lerp(0.85f, 1.15f, pulse);

            // Dynamic light color that shifts slightly
            float lightPulse = 0.5f + 0.5f * MathF.Sin(_time * 3.2f + _pulsePhase);
            Light = new Vector3(
                0.3f + lightPulse * 0.2f,
                0.9f + lightPulse * 0.1f,
                0.5f + lightPulse * 0.3f) * pulse;

            UpdateDynamicLight(pulse, lightPulse);

            // Spawn sparkles periodically
            _sparkleTimer += dt;
            if (_sparkleTimer >= SparkleInterval)
            {
                _sparkleTimer = 0f;
                SpawnSparkle();
            }

            base.Update(gameTime);
        }

        private void SpawnSparkle()
        {
            if (World == null || Status != GameControlStatus.Ready || Hidden)
                return;

            // 50% chance to spawn a sparkle each interval
            if (MuGame.Random.NextDouble() > 0.5)
                return;

            int frame = MuGame.FrameIndex;
            if (frame != _sparkleFrame)
            {
                _sparkleFrame = frame;
                _sparklesThisFrame = 0;
            }
            if (_sparklesThisFrame >= MaxSparklesPerFrame)
                return;
            _sparklesThisFrame++;

            var sparkle = ElfBuffSparkle.Rent(GetCurrentOrbPosition(), _trailHue);
            World.Objects.Add(sparkle);
            if (sparkle.Status == GameControlStatus.NonInitialized)
                _ = sparkle.Load();
        }

        public override float Depth => Position.Y + Position.Z;

        private bool TrySyncOwner()
        {
            if (_owner == null || _owner.Status == GameControlStatus.Disposed || _owner.World == null)
            {
                RemoveSelf();
                return false;
            }

            bool hide = _owner.Hidden || _owner.IsDead || _owner.Status != GameControlStatus.Ready;
            Hidden = hide;
            _trail.Hidden = hide;
            if (hide && _dynamicLight != null)
            {
                _dynamicLight.Intensity = 0f;
            }

            return !hide;
        }

        private void RemoveSelf()
        {
            if (Parent != null)
            {
                Parent.Children.Remove(this);
            }
            else if (World != null)
            {
                World.Objects.Remove(this);
            }

            Dispose();
        }

        private void UpdateDynamicLight(float pulse, float lightPulse)
        {
            if (!_lightAdded && World?.Terrain != null)
            {
                World.Terrain.AddDynamicLight(_dynamicLight);
                _lightAdded = true;
            }

            float flicker = 0.6f + 0.4f * (0.5f + 0.5f * MathF.Sin(_time * 6.5f + _phaseOffset));
            _currentLightIntensity = MathHelper.Lerp(0.65f, 1.3f, pulse) * flicker * 0.5f;
            _dynamicLight.Intensity = _currentLightIntensity;
            _dynamicLight.Position = Position;

            float radiusOffset = MathF.Sin(_time * 3.2f + _phaseOffset) * _lightRadiusJitter;
            _dynamicLight.Radius = Math.Max(95f, _lightBaseRadius + radiusOffset);

            float huePulse = 0.85f + lightPulse * 0.2f;
            _dynamicLight.Color = new Vector3(
                0.22f * _trailHue * huePulse,
                MathHelper.Clamp(0.9f * huePulse, 0.6f, 1.2f),
                0.48f * _trailHue * huePulse);
        }

        public override void Dispose()
        {
            if (_lightAdded && World?.Terrain != null && _dynamicLight != null)
            {
                World.Terrain.RemoveDynamicLight(_dynamicLight);
                _lightAdded = false;
            }

            base.Dispose();
        }
    }
}
