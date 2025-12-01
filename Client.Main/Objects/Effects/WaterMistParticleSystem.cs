using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// High-performance water mist particle system with procedural texture generation.
    /// Optimized for minimal allocations and GPU-friendly rendering.
    /// </summary>
    public class WaterMistParticleSystem : WorldObject
    {
        // Fixed-size particle pool - no allocations during runtime
        private const int MAX_PARTICLES = 256;
        
        // Structure of Arrays for cache coherency
        private readonly Vector3[] _positions = new Vector3[MAX_PARTICLES];
        private readonly Vector3[] _velocities = new Vector3[MAX_PARTICLES];
        private readonly float[] _lives = new float[MAX_PARTICLES];
        private readonly float[] _maxLives = new float[MAX_PARTICLES];
        private readonly float[] _baseScales = new float[MAX_PARTICLES];
        private readonly float[] _rotations = new float[MAX_PARTICLES];
        
        private int _activeCount;
        private float _emissionAccumulator;
        
        // Pre-generated procedural texture
        private Texture2D _texture;
        private Vector2 _textureCenter;
        
        // Cached random for particle emission
        private readonly Random _random = new();
        
        // Cached matrices to avoid per-frame allocations
        private Matrix _viewProj;
        private Vector3 _cameraPos;

        // Cached reflection for OutOfView property
        private static readonly System.Reflection.PropertyInfo _outOfViewProperty =
            typeof(WorldObject).GetProperty("OutOfView");

        // === Tunable Properties ===
        public float EmissionRate { get; set; } = 8f;
        public Vector2 ScaleRange { get; set; } = new(0.4f, 0.8f);
        public Vector2 LifetimeRange { get; set; } = new(2.5f, 4f);
        public Vector2 HorizontalVelocityRange { get; set; } = new(-8f, 8f);
        public Vector2 UpwardVelocityRange { get; set; } = new(12f, 22f);
        public float UpwardAcceleration { get; set; } = 4f;
        public Color ParticleColor { get; set; } = new Color(200, 220, 255, 180);
        public Vector2 SpawnRadius { get; set; } = new(6f, 6f);
        public float MaxDistance { get; set; } = 1500f;
        public float ScaleGrowth { get; set; } = 0.4f;
        public Vector2 Wind { get; set; } = Vector2.Zero;

        public WaterMistParticleSystem()
        {
            // Particle systems render after all solid and transparent objects
            // They use 2D sprites so they don't interact with 3D depth buffer properly
            IsTransparent = false;
            AffectedByTransparency = false;  // Render in "solid in front" pass, after everything else

            // Large local bbox to prevent frustum culling - particles handle their own culling
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-5000, -5000, -5000),
                new Vector3(5000, 5000, 5000)
            );
        }

        public override Task LoadContent()
        {
            GenerateProceduralTexture();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates a soft, glowing particle texture procedurally.
        /// Uses radial gradient with smooth falloff - no external assets needed.
        /// </summary>
        private void GenerateProceduralTexture()
        {
            const int size = 64; // Larger for better quality
            var device = GraphicsManager.Instance.GraphicsDevice;
            _texture = new Texture2D(device, size, size);
            
            var pixels = new Color[size * size];
            float center = size * 0.5f;
            float maxRadius = center;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    
                    // Smooth falloff - brighter in center
                    float t = MathHelper.Clamp(1f - dist / maxRadius, 0f, 1f);
                    
                    // Smoothstep for soft edges
                    float alpha = t * t * (3f - 2f * t);
                    
                    pixels[y * size + x] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255));
                }
            }
            
            _texture.SetData(pixels);
            _textureCenter = new Vector2(size * 0.5f, size * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        /// <summary>
        /// Forces this particle system to be visible by bypassing OutOfView culling.
        /// Uses cached reflection to set the private OutOfView property.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceVisible()
        {
            // Use cached reflection to access the private setter of OutOfView
            _outOfViewProperty?.SetValue(this, false);
        }

        private void EmitParticle()
        {
            if (_activeCount >= MAX_PARTICLES) return;
            
            int i = _activeCount++;
            
            _positions[i] = new Vector3(
                Position.X + RandomRange(-SpawnRadius.X, SpawnRadius.X),
                Position.Y + RandomRange(-SpawnRadius.Y, SpawnRadius.Y),
                Position.Z
            );
            
            _velocities[i] = new Vector3(
                RandomRange(HorizontalVelocityRange.X, HorizontalVelocityRange.Y),
                RandomRange(HorizontalVelocityRange.X, HorizontalVelocityRange.Y),
                RandomRange(UpwardVelocityRange.X, UpwardVelocityRange.Y)
            );
            
            _maxLives[i] = RandomRange(LifetimeRange.X, LifetimeRange.Y);
            _lives[i] = _maxLives[i];
            _baseScales[i] = RandomRange(ScaleRange.X, ScaleRange.Y);
            _rotations[i] = RandomRange(0, MathHelper.TwoPi);
        }

        public override void Update(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready) return;
            if (Camera.Instance == null) return;

            // Lazy initialization - generate texture on first update if it failed during Load
            if (_texture == null)
            {
                GenerateProceduralTexture();
            }

            // Force visibility by setting OutOfView = false every frame
            // This is necessary because WorldObject's culling system would skip our Update()
            ForceVisible();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Cache camera data once per frame
            _cameraPos = Camera.Instance.Position;

            // Distance-based emission culling
            float distToCamera = Vector3.DistanceSquared(_cameraPos, Position);
            float maxDistSq = MaxDistance * MaxDistance;

            if (distToCamera > maxDistSq)
            {
                // Too far - skip emission but still update existing particles
                UpdateParticles(dt);
                return;
            }

            // Scale emission rate by distance (squared falloff)
            float distRatio = distToCamera / maxDistSq;
            float emissionScale = 1f - distRatio * distRatio;

            _emissionAccumulator += EmissionRate * emissionScale * dt;
            int emitCount = (int)_emissionAccumulator;
            _emissionAccumulator -= emitCount;

            for (int i = 0; i < emitCount; i++)
                EmitParticle();

            UpdateParticles(dt);
        }

        private void UpdateParticles(float dt)
        {
            float windX = Wind.X * dt;
            float windY = Wind.Y * dt;
            float accelDt = UpwardAcceleration * dt;
            
            int i = 0;
            while (i < _activeCount)
            {
                _lives[i] -= dt;
                
                if (_lives[i] <= 0f)
                {
                    // Swap-and-pop: O(1) removal
                    int last = --_activeCount;
                    if (i != last)
                    {
                        _positions[i] = _positions[last];
                        _velocities[i] = _velocities[last];
                        _lives[i] = _lives[last];
                        _maxLives[i] = _maxLives[last];
                        _baseScales[i] = _baseScales[last];
                        _rotations[i] = _rotations[last];
                    }
                    continue; // Don't increment - check swapped particle
                }
                
                // Update position
                ref var pos = ref _positions[i];
                ref var vel = ref _velocities[i];
                
                pos.X += vel.X * dt + windX;
                pos.Y += vel.Y * dt + windY;
                pos.Z += vel.Z * dt;
                vel.Z += accelDt;
                
                i++;
            }
        }

        public override float Depth
        {
            get
            {
                // Return large depth so particles render after most 3D objects
                // Depth is Y + Z, so use very large value
                return Position.Y + Position.Z + 10000f;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (_activeCount == 0 || _texture == null) return;
            if (Status != GameControlStatus.Ready) return;

            var camera = Camera.Instance;
            if (camera == null) return;

            var device = GraphicsManager.Instance.GraphicsDevice;
            var spriteBatch = GraphicsManager.Instance.Sprite;

            // Cache view-projection
            _viewProj = camera.View * camera.Projection;
            _cameraPos = camera.Position;
            
            // Save states
            var prevBlend = device.BlendState;
            var prevDepth = device.DepthStencilState;
            var prevRaster = device.RasterizerState;
            var prevSampler = device.SamplerStates[0];
            
            // Begin batch with Deferred (no sorting overhead)
            // Use DepthRead to respect existing depth buffer from 3D objects
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.DepthRead,  // Read depth but don't write
                RasterizerState.CullNone
            );
            
            var viewport = device.Viewport;
            float maxDistSq = MaxDistance * MaxDistance;
            Vector3 camForward = Vector3.Normalize(camera.Target - _cameraPos);
            
            for (int i = 0; i < _activeCount; i++)
            {
                ref var pos = ref _positions[i];
                
                // Fast frustum check - skip particles behind camera
                Vector3 toParticle = pos - _cameraPos;
                if (Vector3.Dot(toParticle, camForward) < 0) continue;
                
                // Distance-based culling (squared, no sqrt)
                float distSq = toParticle.LengthSquared();
                if (distSq > maxDistSq) continue;
                
                // Project to screen (optimized inline)
                Vector4 clipPos = Vector4.Transform(pos, _viewProj);
                
                if (clipPos.W <= 0.001f) continue; // Behind near plane
                
                float invW = 1f / clipPos.W;
                float screenX = (clipPos.X * invW * 0.5f + 0.5f) * viewport.Width;
                float screenY = (0.5f - clipPos.Y * invW * 0.5f) * viewport.Height;
                float depth = clipPos.Z * invW;

                if (depth < 0f || depth > 1f) continue;

                // Depth test against 3D scene - skip if particle is behind solid objects
                // Note: This is approximate since we can't read depth buffer in SpriteBatch
                // A better solution would be to render particles as billboards in 3D space
                
                // Screen bounds check
                if (screenX < -100 || screenX > viewport.Width + 100 ||
                    screenY < -100 || screenY > viewport.Height + 100) continue;
                
                // Calculate alpha and scale
                float lifeRatio = _lives[i] / _maxLives[i];
                float alpha = lifeRatio * lifeRatio; // Quadratic fade
                
                // Distance-based scale (using squared distance, approximation)
                float distFactor = distSq / maxDistSq;
                float depthScale = MathHelper.Lerp(1f, 0.5f, distFactor);  // Zmienione z 0.3 na 0.5 (mniejsza redukcja)

                // Growth over lifetime
                float growth = 1f + ScaleGrowth * (1f - lifeRatio);
                float finalScale = _baseScales[i] * growth * depthScale;
                
                // Color with alpha
                Color color = ParticleColor * alpha;

                spriteBatch.Draw(
                    _texture,
                    new Vector2(screenX, screenY),
                    null,
                    color,
                    _rotations[i],
                    _textureCenter,
                    finalScale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            spriteBatch.End();
            
            // Restore states
            device.BlendState = prevBlend;
            device.DepthStencilState = prevDepth;
            device.RasterizerState = prevRaster;
            device.SamplerStates[0] = prevSampler;
        }

        public override void Dispose()
        {
            _texture?.Dispose();
            _texture = null;
            base.Dispose();
        }
    }
}
