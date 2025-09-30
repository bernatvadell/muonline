using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Atlans
{
    /// <summary>
    /// Represents a swimming fish in the Atlans underwater world.
    /// Uses velocity-based physics with momentum for realistic swimming behavior.
    /// </summary>
    public class FishObject : ModelObject
    {
        private Random _random = new Random();
        private float _timer;
        private float _baseSpeed; // Desired swimming speed
        private float _targetHeight;
        private float _directionChangeTimer;

        // NEW VELOCITY-BASED SYSTEM
        private Vector3 _currentVelocity; // ACTUAL velocity with momentum (units/sec)
        private Vector3 _desiredDirection; // Target direction (normalized) from Boid/behavior
        private float _wanderOffset; // Sinusoidal offset for curved swimming
        private float _currentSpeed; // Current speed (varies over time)
        private float _targetSpeed; // Target speed to lerp towards

        private const float DirectionChangeInterval = 3f; // Change direction every 4 seconds
        private const float SteeringSpeed = 1.2f; // How fast fish can change direction
        private const float RotationSpeed = 2.0f; // How fast visual model rotates
        private const float WanderStrength = 1f; // How much fish meanders (0 = straight, 1 = very curved)
        private const float WanderFrequency = 1.5f; // How fast the meandering oscillates
        private const float MinSpeedMultiplier = 1.2f; // Minimum speed
        private const float MaxSpeedMultiplier = 2.4f; // Maximum speed

        private const float SwimHeight = 80f; // Fixed swim height above terrain

        // Legacy properties for BoidManager compatibility
        public Vector3 Direction
        {
            get => _desiredDirection;
            set => _desiredDirection = value.LengthSquared() > 0.001f ? Vector3.Normalize(value) : value;
        }
        public float Velocity { get; set; } // Not used in new system
        public float Timer
        {
            get => _timer;
            set => _timer = value;
        }

        public int SubType { get; set; }
        public int LifeTime { get; set; }

        public FishObject()
        {
            _timer = (float)(_random.NextDouble() * MathF.PI * 2); // Random phase for wander
            _baseSpeed = 30f + (float)_random.NextDouble() * 20f; // 30-50 units/sec
            _currentSpeed = _baseSpeed;
            _targetSpeed = _baseSpeed;
            // Scale is set by BoidManager during spawn (random 1.7-2.3)
            Alpha = 1f;
            LightEnabled = false;
            BlendState = BlendState.AlphaBlend;
            IsTransparent = true;
            DepthState = DepthStencilState.DepthRead;
            _targetHeight = SwimHeight;
            _directionChangeTimer = 0f;
            _wanderOffset = 0f;

            // Initialize velocity system
            _currentVelocity = Vector3.Zero;
            _desiredDirection = Vector3.Zero;

            SubType = 0;
            LifeTime = 0;
        }

        public override async Task Load()
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            int fishModelIndex = Type + 1;
            var modelPath = $"Object8/Fish{fishModelIndex.ToString().PadLeft(2, '0')}.bmd";

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model != null)
            {
                CurrentAction = 0;
                AnimationSpeed = 4.5f;
            }

            await base.Load();

            // Initialize velocity to match initial angle
            float angleRad = Angle.Z;
            _desiredDirection = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);
            _currentVelocity = _desiredDirection * _currentSpeed;
        }

        public override void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized)
            {
                _ = Load();
                return;
            } 

            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Fade in alpha
            if (Alpha < 1f)
            {
                Alpha += deltaTime * 2f;
                if (Alpha > 1f) Alpha = 1f;
            }
        }

        /// <summary>
        /// Updates desired swimming direction based on behavior and environment.
        /// Called by BoidManager - modifies _desiredDirection, speed, and wander.
        /// Fish swim freely in random directions, not following the hero.
        /// </summary>
        public void UpdateMovement(GameTime gameTime, Vector3 heroPosition, float terrainHeight)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _timer += deltaTime;
            _directionChangeTimer += deltaTime;

            // Update target height
            _targetHeight = terrainHeight + SwimHeight;

            // SPEED VARIATION - fish speed up and slow down naturally
            // Change target speed periodically
            if (_random.NextDouble() < deltaTime * 0.3f) // 30% chance per second
            {
                float speedMultiplier = MinSpeedMultiplier + (float)_random.NextDouble() * (MaxSpeedMultiplier - MinSpeedMultiplier);
                _targetSpeed = _baseSpeed * speedMultiplier;
            }

            // Smoothly lerp current speed to target speed
            _currentSpeed = MathHelper.Lerp(_currentSpeed, _targetSpeed, MathHelper.Clamp(deltaTime * 0.5f, 0f, 1f));

            // Check distance from hero for despawn (handled by BoidManager)
            Vector2 toHero = new Vector2(heroPosition.X - Position.X, heroPosition.Y - Position.Y);
            float distance = toHero.Length();

            // Build desired direction from behaviors
            Vector3 behaviorInfluence = Vector3.Zero;

            // 1. Random wander - change direction periodically (main behavior)
            if (_directionChangeTimer >= DirectionChangeInterval)
            {
                _directionChangeTimer = 0f;
                float randomAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                // Gentle random influence for smooth direction changes
                behaviorInfluence += new Vector3(MathF.Cos(randomAngle), MathF.Sin(randomAngle), 0f) * 0.8f; // REDUCED from 2.0f
            }

            // 2. SINUSOIDAL WANDER - adds natural curved swimming paths
            // Calculate perpendicular direction to current velocity for side-to-side movement
            if (_currentVelocity.LengthSquared() > 0.1f)
            {
                Vector3 forward = Vector3.Normalize(_currentVelocity);
                Vector3 perpendicular = new Vector3(-forward.Y, forward.X, 0f); // Rotate 90 degrees

                // Sinusoidal offset for smooth S-curves
                _wanderOffset = MathF.Sin(_timer * WanderFrequency) * WanderStrength;
                behaviorInfluence += perpendicular * _wanderOffset;
            }

            // Update desired direction (will be applied with steering in ApplyMovement)
            if (behaviorInfluence.LengthSquared() > 0.001f)
            {
                // Blend current desired direction with behavior influence
                _desiredDirection += behaviorInfluence * deltaTime;

                // Normalize to keep it as unit vector
                if (_desiredDirection.LengthSquared() > 0.001f)
                {
                    _desiredDirection = Vector3.Normalize(_desiredDirection);
                }
            }

            // If desired direction is still zero, use current velocity direction
            if (_desiredDirection.LengthSquared() < 0.001f && _currentVelocity.LengthSquared() > 0.001f)
            {
                _desiredDirection = Vector3.Normalize(_currentVelocity);
            }
        }

        /// <summary>
        /// Returns distance from hero (used by BoidManager for despawning)
        /// </summary>
        public float GetDistanceFromHero(Vector3 heroPosition)
        {
            return Vector2.Distance(
                new Vector2(Position.X, Position.Y),
                new Vector2(heroPosition.X, heroPosition.Y));
        }

        /// <summary>
        /// Applies velocity-based movement with momentum and smooth rotation.
        /// Uses MU Online's isometric Direction system for correct visual orientation.
        /// </summary>
        public void ApplyMovement(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Calculate target velocity (where we WANT to go) using CURRENT speed (varies)
            Vector3 targetVelocity = _desiredDirection * _currentSpeed;

            // Smoothly steer current velocity towards target velocity (MOMENTUM!)
            // Lower SteeringSpeed = smoother turns, higher = sharper turns
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity,
                MathHelper.Clamp(deltaTime * SteeringSpeed, 0f, 1f));

            // DYNAMIC ANIMATION SPEED - tail wags faster when swimming faster
            float actualSpeed = _currentVelocity.Length();
            float speedRatio = actualSpeed / _baseSpeed; // 0.6 to 1.4
            AnimationSpeed = MathHelper.Clamp(1.5f + speedRatio * 2.0f, 1.0f, 4.5f); // 1.0 to 4.5

            // Apply movement
            float newZ = MathHelper.Lerp(Position.Z, _targetHeight,
                MathHelper.Clamp(deltaTime * 0.5f, 0f, 1f));

            Position = new Vector3(
                Position.X + _currentVelocity.X * deltaTime,
                Position.Y + _currentVelocity.Y * deltaTime,
                newZ
            );

            // Update visual orientation - CONTINUOUS angle from velocity (not discrete Direction enum)
            if (_currentVelocity.LengthSquared() > 1.0f) // Only rotate if moving significantly
            {
                // Calculate CONTINUOUS target angle directly from velocity vector
                // This prevents discrete jumps and keeps rotation synchronized with movement
                float targetAngleRaw = MathF.Atan2(_currentVelocity.Y, _currentVelocity.X);

                // MU Online isometric mapping:
                // XNA: atan2(+Y, +X) = 45° (southeast in standard coords)
                // MU:  Direction.East = 135° (when dx=+1, dy=+1)
                // Offset = 135° - 45° = +90° = π/2
                float targetAngleIsometric = targetAngleRaw + MathHelper.PiOver2;

                float currentAngle = Angle.Z;

                // Normalize angle difference to -π to π for shortest rotation path
                float angleDiff = MathHelper.WrapAngle(targetAngleIsometric - currentAngle);

                // SMOOTH rotation - limit rotation speed per frame
                float maxRotationStep = RotationSpeed * deltaTime;
                float rotationStep = MathHelper.Clamp(angleDiff, -maxRotationStep, maxRotationStep);

                Angle = new Vector3(Angle.X, Angle.Y, currentAngle + rotationStep);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Model == null)
                return;

            base.Draw(gameTime);
        }
    }
}