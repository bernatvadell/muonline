using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Noria
{
    /// <summary>
    /// Represents a flying butterfly in the Noria world.
    /// Uses 3D velocity-based physics with vertical flutter for realistic flight behavior.
    /// </summary>
    public class ButterflyObject : ModelObject
    {
        private Random _random = new Random();
        private float _timer;
        private float _baseSpeed;
        private float _directionChangeTimer;
        private float _verticalFlutterPhase;

        // 3D VELOCITY SYSTEM (includes vertical movement)
        private Vector3 _currentVelocity; // Actual 3D velocity with momentum
        private Vector3 _desiredDirection; // Target direction (normalized, includes Z)
        private float _currentSpeed;
        private float _targetSpeed;
        private float _targetHeight; // Target height above terrain
        private float _flutterOffset; // Sinusoidal vertical flutter

        private const float DirectionChangeInterval = 0.3f; // Change direction every 0.3 seconds (extremely frequent!)
        private const float SteeringSpeed = 2.5f; // How fast butterfly can change direction
        private const float RotationSpeed = 5.0f; // How fast visual model rotates (very fast)
        private const float FlutterStrength = 12f; // Vertical flutter amplitude
        private const float FlutterFrequency = 5.0f; // Wing flapping speed (very fast)
        private const float MinSpeedMultiplier = 1.5f; // Faster baseline
        private const float MaxSpeedMultiplier = 3.2f; // Faster max
        private const float MinFlightHeight = 70f; // Very low minimum height
        private const float MaxFlightHeight = 160f; // Lower maximum height

        // Legacy properties for compatibility
        public Vector3 Direction
        {
            get => _desiredDirection;
            set => _desiredDirection = value.LengthSquared() > 0.001f ? Vector3.Normalize(value) : value;
        }
        public float Velocity { get; set; }

        public float Timer
        {
            get => _timer;
            set => _timer = value;
        }

        public int SubType { get; set; }

        public ButterflyObject()
        {
            _timer = (float)(_random.NextDouble() * MathF.PI * 2);
            _verticalFlutterPhase = (float)(_random.NextDouble() * MathF.PI * 2);
            _baseSpeed = 60f + (float)_random.NextDouble() * 40f; // 60-100 units/sec (much faster)
            _currentSpeed = _baseSpeed;
            _targetSpeed = _baseSpeed;
            // Scale is set by ButterflyManager during spawn (random 2.0-2.4)
            Alpha = 1f;
            LightEnabled = false;
            BlendState = BlendState.AlphaBlend;
            IsTransparent = true;
            DepthState = DepthStencilState.DepthRead;
            _directionChangeTimer = 0f;
            _flutterOffset = 0f;

            // Initialize velocity system
            _currentVelocity = Vector3.Zero;
            _desiredDirection = Vector3.Zero;
            _targetHeight = MinFlightHeight + (float)_random.NextDouble() * (MaxFlightHeight - MinFlightHeight);

            SubType = 0;
        }

        public override async Task Load()
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            var modelPath = "Object1/Butterfly01.bmd";
            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model != null)
            {
                CurrentAction = 0;
                AnimationSpeed = 15.0f; // Very fast wing flapping
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
        /// Updates desired flight direction and vertical behavior.
        /// Butterflies fly more chaotically than fish with frequent direction changes.
        /// </summary>
        public void UpdateMovement(GameTime gameTime, Vector3 heroPosition, float terrainHeight)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _timer += deltaTime;
            _verticalFlutterPhase += deltaTime * FlutterFrequency;
            _directionChangeTimer += deltaTime;

            // SPEED VARIATION - butterflies speed up and slow down constantly
            if (_random.NextDouble() < deltaTime * 1.5f) // Very frequent speed changes
            {
                float speedMultiplier = MinSpeedMultiplier + (float)_random.NextDouble() * (MaxSpeedMultiplier - MinSpeedMultiplier);
                _targetSpeed = _baseSpeed * speedMultiplier;
            }

            _currentSpeed = MathHelper.Lerp(_currentSpeed, _targetSpeed, MathHelper.Clamp(deltaTime * 1.2f, 0f, 1f));

            // MAJOR DIRECTION CHANGE - completely reorient butterfly
            if (_directionChangeTimer >= DirectionChangeInterval)
            {
                _directionChangeTimer = 0f;

                // COMPLETELY REPLACE direction with random new one
                float randomAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 newDirection = new Vector3(MathF.Cos(randomAngle), MathF.Sin(randomAngle), 0f);

                // Blend 70% new direction with 30% old for some continuity
                _desiredDirection = Vector3.Lerp(_desiredDirection, newDirection, 0.7f);
                if (_desiredDirection.LengthSquared() > 0.001f)
                {
                    _desiredDirection = Vector3.Normalize(_desiredDirection);
                }

                // Also randomize target height
                _targetHeight = MinFlightHeight + (float)_random.NextDouble() * (MaxFlightHeight - MinFlightHeight);
            }

            // CONSTANT CHAOTIC NUDGES - frequent small direction changes (NO deltaTime multiply!)
            if (_random.NextDouble() < 0.5f) // 50% chance every frame at 60fps = 30 nudges/sec
            {
                float nudgeAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 nudge = new Vector3(MathF.Cos(nudgeAngle), MathF.Sin(nudgeAngle), 0f);

                // Add nudge with significant strength (NOT multiplied by deltaTime!)
                _desiredDirection = Vector3.Lerp(_desiredDirection, nudge, 0.15f); // 15% towards random direction
                if (_desiredDirection.LengthSquared() > 0.001f)
                {
                    _desiredDirection = Vector3.Normalize(_desiredDirection);
                }
            }

            // VERTICAL FLUTTER - wing flapping causes up/down movement
            _flutterOffset = MathF.Sin(_verticalFlutterPhase) * FlutterStrength;

            // If desired direction is still zero, use current velocity direction
            if (_desiredDirection.LengthSquared() < 0.001f && _currentVelocity.LengthSquared() > 0.001f)
            {
                _desiredDirection = Vector3.Normalize(_currentVelocity);
            }
        }

        /// <summary>
        /// Returns distance from hero (used by ButterflyManager for despawning)
        /// </summary>
        public float GetDistanceFromHero(Vector3 heroPosition)
        {
            return Vector2.Distance(
                new Vector2(Position.X, Position.Y),
                new Vector2(heroPosition.X, heroPosition.Y));
        }

        /// <summary>
        /// Applies velocity-based 3D movement with vertical flutter.
        /// Maintains height constraints (50-300 above terrain).
        /// </summary>
        public void ApplyMovement(GameTime gameTime, float terrainHeight)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Calculate target velocity (horizontal movement)
            Vector3 targetVelocity = _desiredDirection * _currentSpeed;

            // Smoothly steer current velocity towards target velocity
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity,
                MathHelper.Clamp(deltaTime * SteeringSpeed, 0f, 1f));

            // DYNAMIC ANIMATION SPEED - wings flap faster when flying faster
            float actualSpeed = _currentVelocity.Length();
            float speedRatio = actualSpeed / _baseSpeed;
            AnimationSpeed = MathHelper.Clamp(10.0f + speedRatio * 8.0f, 8.0f, 20.0f); // Much faster wing flapping!

            // Apply horizontal movement
            Position = new Vector3(
                Position.X + _currentVelocity.X * deltaTime,
                Position.Y + _currentVelocity.Y * deltaTime,
                Position.Z
            );

            // VERTICAL MOVEMENT with flutter and height constraints
            float desiredZ = terrainHeight + _targetHeight + _flutterOffset;

            // Add frequent random vertical impulses for chaotic vertical movement
            if (_random.NextDouble() < deltaTime * 10f) // Very frequent vertical changes
            {
                desiredZ += (float)(_random.NextDouble() * 8f - 4f); // ±4 units random bounce (increased)
            }

            // Random height target changes
            if (_random.NextDouble() < deltaTime * 2f) // Change target height frequently
            {
                _targetHeight = MinFlightHeight + (float)_random.NextDouble() * (MaxFlightHeight - MinFlightHeight);
            }

            // Smoothly lerp to desired height
            Position = new Vector3(Position.X, Position.Y,
                MathHelper.Lerp(Position.Z, desiredZ, MathHelper.Clamp(deltaTime * 1.5f, 0f, 1f)));

            // Update visual orientation - continuous angle from velocity
            if (_currentVelocity.LengthSquared() > 1.0f)
            {
                float targetAngleRaw = MathF.Atan2(_currentVelocity.Y, _currentVelocity.X);
                float targetAngleIsometric = targetAngleRaw + MathHelper.PiOver2;

                float currentAngle = Angle.Z;
                float angleDiff = MathHelper.WrapAngle(targetAngleIsometric - currentAngle);
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
