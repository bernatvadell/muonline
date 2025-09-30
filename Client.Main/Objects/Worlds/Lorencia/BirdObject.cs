using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    /// <summary>
    /// Represents a flying bird in Lorencia world.
    /// Birds have 4 AI states: Flying (200-600 height), Descending, OnGround, Ascending
    /// Based on original client's MoveBird algorithm with BOID_FLY/DOWN/GROUND/UP states.
    /// </summary>
    public class BirdObject : ModelObject
    {
        private Random _random = new Random();
        private float _timer;
        private float _baseSpeed;
        private float _directionChangeTimer;

        // 3D VELOCITY SYSTEM
        private Vector3 _currentVelocity;
        private Vector3 _desiredDirection;
        private float _currentSpeed;
        private float _targetSpeed;
        private float _targetHeight;

        // AI STATE SYSTEM
        private BirdAIState _aiState;
        private float _stateTimer; // Time spent in current state

        private const float DirectionChangeInterval = 1.0f; // Change direction frequently for chaotic flight
        private const float SteeringSpeed = 2.5f; // Fast steering for erratic movement
        private const float RotationSpeed = 4.0f;
        private const float MinSpeedMultiplier = 1.0f;
        private const float MaxSpeedMultiplier = 1.3f;
        private const float MinFlightHeight = 350f; // Birds fly high (original: 200-600)
        private const float MaxFlightHeight = 450f;
        private const float LandCheckDistance = 200f; // Start checking for landing at 200 units
        private const float LandCheckMaxDistance = 400f; // Stop checking at 400 units
        private const float VerticalLerpSpeed = 0.3f; // Moderate vertical movement
        private const float GroundOffset = 65f; // Height above terrain when on ground (increased to prevent underground)

        public enum BirdAIState
        {
            Flying,      // Normal flight at 200-600 height
            Descending,  // Flying down to ground
            OnGround,    // Resting on terrain
            Ascending    // Taking off from ground
        }

        // Legacy properties
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
        public BirdAIState AIState => _aiState;

        public BirdObject()
        {
            _timer = (float)(_random.NextDouble() * MathF.PI * 2);
            _baseSpeed = 600f + (float)_random.NextDouble() * 50f; // 100-150 units/sec (much faster!)
            _currentSpeed = _baseSpeed;
            _targetSpeed = _baseSpeed;
            // Scale is set by BirdManager during spawn
            Alpha = 1f;
            LightEnabled = true; // Enable lighting for shadows
            BlendState = BlendState.AlphaBlend;
            IsTransparent = true;
            RenderShadow = true;
            DepthState = DepthStencilState.Default; // Use default depth state for proper rendering
            _directionChangeTimer = 0f;

            // Initialize velocity system
            _currentVelocity = Vector3.Zero;
            _desiredDirection = Vector3.Zero;
            _targetHeight = MinFlightHeight + (float)_random.NextDouble() * (MaxFlightHeight - MinFlightHeight);

            // Start in flying state
            _aiState = BirdAIState.Flying;
            _stateTimer = 0f;

            SubType = 0;
        }

        public override async Task Load()
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            var modelPath = "Object1/Bird01.bmd";
            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model != null)
            {
                CurrentAction = 0;
                AnimationSpeed = 12.0f; // Wing flapping speed
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
        /// Updates bird behavior based on AI state and hero position.
        /// Birds periodically land when hero is at distance 200-400.
        /// </summary>
        public void UpdateMovement(GameTime gameTime, Vector3 heroPosition)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _timer += deltaTime;
            _stateTimer += deltaTime;
            _directionChangeTimer += deltaTime;

            float distanceFromHero = Vector2.Distance(
                new Vector2(Position.X, Position.Y),
                new Vector2(heroPosition.X, heroPosition.Y));

            // STATE MACHINE
            switch (_aiState)
            {
                case BirdAIState.Flying:
                    UpdateFlyingState(deltaTime, distanceFromHero);
                    break;

                case BirdAIState.Descending:
                    UpdateDescendingState(deltaTime);
                    break;

                case BirdAIState.OnGround:
                    UpdateGroundState(deltaTime, heroPosition);
                    break;

                case BirdAIState.Ascending:
                    UpdateAscendingState(deltaTime);
                    break;
            }
        }

        private void UpdateFlyingState(float deltaTime, float distanceFromHero)
        {
            // Occasional chirping while flying
            if (_random.NextDouble() < deltaTime * 0.05f) // 5% chance per second
            {
                PlayBirdSound();
            }

            // Speed variation
            if (_random.NextDouble() < deltaTime * 0.5f)
            {
                float speedMultiplier = MinSpeedMultiplier + (float)_random.NextDouble() * (MaxSpeedMultiplier - MinSpeedMultiplier);
                _targetSpeed = _baseSpeed * speedMultiplier;
            }

            _currentSpeed = MathHelper.Lerp(_currentSpeed, _targetSpeed, MathHelper.Clamp(deltaTime * 0.8f, 0f, 1f));

            // Direction changes (less chaotic than butterflies)
            if (_directionChangeTimer >= DirectionChangeInterval)
            {
                _directionChangeTimer = 0f;
                float randomAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 newDirection = new Vector3(MathF.Cos(randomAngle), MathF.Sin(randomAngle), 0f);
                _desiredDirection = Vector3.Lerp(_desiredDirection, newDirection, 0.5f);
                if (_desiredDirection.LengthSquared() > 0.001f)
                {
                    _desiredDirection = Vector3.Normalize(_desiredDirection);
                }

                // Change target height within flight range
                _targetHeight = MinFlightHeight + (float)_random.NextDouble() * (MaxFlightHeight - MinFlightHeight);
            }

            // CHAOTIC MOVEMENT - frequent erratic direction changes like butterflies
            if (_random.NextDouble() < 0.4f) // 40% chance per frame for chaotic flight
            {
                float nudgeAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 nudge = new Vector3(MathF.Cos(nudgeAngle), MathF.Sin(nudgeAngle), 0f);
                _desiredDirection = Vector3.Lerp(_desiredDirection, nudge, 0.2f); // Stronger nudge
                if (_desiredDirection.LengthSquared() > 0.001f)
                {
                    _desiredDirection = Vector3.Normalize(_desiredDirection);
                }
            }

            // Additional erratic movement
            if (_random.NextDouble() < deltaTime * 8f) // Frequent small adjustments
            {
                float erraticAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 erratic = new Vector3(MathF.Cos(erraticAngle), MathF.Sin(erraticAngle), 0f);
                _desiredDirection = Vector3.Lerp(_desiredDirection, erratic, 0.12f);
                if (_desiredDirection.LengthSquared() > 0.001f)
                {
                    _desiredDirection = Vector3.Normalize(_desiredDirection);
                }
            }

            // CHECK FOR LANDING - if hero is at distance 200-400, occasionally land
            // Original: if ((int)WorldTime % 8192 < 2048) - roughly 25% of time
            if (_stateTimer > 5f && // Don't check immediately after state change
                distanceFromHero >= LandCheckDistance &&
                distanceFromHero <= LandCheckMaxDistance)
            {
                if (_random.NextDouble() < deltaTime * 0.3f) // 30% chance per second
                {
                    _aiState = BirdAIState.Descending;
                    _stateTimer = 0f;
                }
            }
        }

        private void UpdateDescendingState(float deltaTime)
        {
            // Descend rapidly - o->Direction[2] = -20.f
            _currentSpeed = _baseSpeed * 1.0f; // Maintain speed
            // No horizontal direction changes while descending
        }

        private void UpdateGroundState(float deltaTime, Vector3 heroPosition)
        {
            // On ground - wait for hero to approach or random takeoff
            _currentSpeed = 0f; // Stopped

            // Check if hero is moving (walking) - would need hero state, simplified to distance check
            float distanceFromHero = Vector2.Distance(
                new Vector2(Position.X, Position.Y),
                new Vector2(heroPosition.X, heroPosition.Y));

            // Take off if hero gets close OR random chance
            if (distanceFromHero < 150f || _random.NextDouble() < deltaTime * 0.1f) // 10% chance per second
            {
                _aiState = BirdAIState.Ascending;
                _stateTimer = 0f;
                _currentSpeed = _baseSpeed * 1.1f; // Boost speed for takeoff
                CurrentAction = 0; // Reset animation
            }
        }

        private void UpdateAscendingState(float deltaTime)
        {
            // Ascending - velocity slowly decreases
            _currentSpeed -= 0.005f * 60f * deltaTime; // Original: 0.005f * FPS_ANIMATION_FACTOR

            // Random height fluctuations during ascent
            if (_random.NextDouble() < deltaTime * 10f)
            {
                _targetHeight += (float)(_random.NextDouble() * 16f - 8f);
            }

            // When velocity reaches normal, switch to flying
            if (_currentSpeed <= _baseSpeed)
            {
                _currentSpeed = _baseSpeed;
                _aiState = BirdAIState.Flying;
                _stateTimer = 0f;
            }
        }

        /// <summary>
        /// Returns distance from hero (used by BirdManager for despawning)
        /// </summary>
        public float GetDistanceFromHero(Vector3 heroPosition)
        {
            return Vector2.Distance(
                new Vector2(Position.X, Position.Y),
                new Vector2(heroPosition.X, heroPosition.Y));
        }

        /// <summary>
        /// Applies velocity-based movement with AI state-specific behavior.
        /// Uses World.Terrain.RequestTerrainHeight for accurate terrain elevation.
        /// </summary>
        public void ApplyMovement(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Get terrain height at bird's current position using World.Terrain.RequestTerrainHeight
            var walkableWorld = World as Controls.WalkableWorldControl;
            if (walkableWorld?.Terrain == null)
                return;

            float terrainHeight = walkableWorld.Terrain.RequestTerrainHeight(Position.X, Position.Y);

            // Calculate target velocity (horizontal movement)
            Vector3 targetVelocity = _desiredDirection * _currentSpeed;

            // Smoothly steer current velocity towards target velocity
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity,
                MathHelper.Clamp(deltaTime * SteeringSpeed, 0f, 1f));

            // Dynamic animation speed based on state
            float actualSpeed = _currentVelocity.Length();
            if (_aiState == BirdAIState.OnGround)
            {
                AnimationSpeed = 1.0f; // Slow idle animation
            }
            else
            {
                float speedRatio = actualSpeed / _baseSpeed;
                AnimationSpeed = MathHelper.Clamp(5.0f + speedRatio * 4.0f, 3.0f, 10.0f);
            }

            // Apply horizontal movement
            Position = new Vector3(
                Position.X + _currentVelocity.X * deltaTime,
                Position.Y + _currentVelocity.Y * deltaTime,
                Position.Z
            );

            // VERTICAL MOVEMENT based on AI state
            float desiredZ = 0f;

            switch (_aiState)
            {
                case BirdAIState.Flying:
                    // Random height fluctuations: o->Position[2] += (float)(rand()%16-8)
                    if (_random.NextDouble() < deltaTime * 5f)
                    {
                        _targetHeight += (float)(_random.NextDouble() * 16f - 8f);
                    }

                    // Clamp to flight range
                    _targetHeight = MathHelper.Clamp(_targetHeight, MinFlightHeight, MaxFlightHeight);
                    desiredZ = terrainHeight + _targetHeight;

                    // SLOW smooth lerp to target height for natural flight
                    Position = new Vector3(Position.X, Position.Y,
                        MathHelper.Lerp(Position.Z, desiredZ, MathHelper.Clamp(deltaTime * VerticalLerpSpeed, 0f, 1f)));
                    break;

                case BirdAIState.Descending:
                    // Slow descent - 3-4x slower (was 20, now 5)
                    float newDescendZ = Position.Z - 5f * deltaTime * 60f;
                    float groundLevel = terrainHeight + GroundOffset;

                    // Never go below ground
                    if (newDescendZ <= groundLevel)
                    {
                        Position = new Vector3(Position.X, Position.Y, groundLevel);
                        _aiState = BirdAIState.OnGround;
                        _stateTimer = 0f;

                        // Play landing sound
                        PlayBirdSound();
                    }
                    else
                    {
                        Position = new Vector3(Position.X, Position.Y, newDescendZ);
                    }
                    break;

                case BirdAIState.OnGround:
                    // Stay on ground at terrain level - recalculate each frame for terrain changes
                    Position = new Vector3(Position.X, Position.Y, terrainHeight + GroundOffset);
                    break;

                case BirdAIState.Ascending:
                    // Slow ascent - 3-4x slower (was 20, now 5)
                    Position = new Vector3(Position.X, Position.Y, Position.Z + 5f * deltaTime * 60f);

                    // Play takeoff sound
                    if (_stateTimer < deltaTime * 2f) // Only once at start
                    {
                        PlayBirdSound();
                    }
                    break;
            }

            // Update visual orientation
            if (_currentVelocity.LengthSquared() > 1.0f && _aiState != BirdAIState.OnGround)
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

        /// <summary>
        /// Plays random bird chirp sound with 3D positional audio
        /// Original: LoadWaveFile(SOUND_BIRD01/02, L"Data\\Sound\\aBird1.wav/aBird2.wav")
        /// </summary>
        private void PlayBirdSound()
        {
            try
            {
                var walkableWorld = World as Controls.WalkableWorldControl;
                if (walkableWorld?.Walker == null)
                    return;

                // Random choice between two bird sounds
                string soundPath = _random.Next(0, 2) == 0
                    ? "Sound/aBird1.wav"
                    : "Sound/aBird2.wav";

                // Play with 3D attenuation - sound gets quieter with distance
                Controllers.SoundController.Instance.PlayBufferWithAttenuation(
                    soundPath,
                    Position,
                    walkableWorld.Walker.Position,
                    maxDistance: 1500f, // Hear birds from far away
                    loop: false
                );
            }
            catch
            {
                // Ignore if sound files don't exist
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
