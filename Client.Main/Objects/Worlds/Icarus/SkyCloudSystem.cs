using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Icarus
{
    public class SkyCloudParticle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
        public Color Color;
        public float Scale;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha;
        public float BaseAlpha;
        public float AlphaVariation;
        public float FadeInTime;
        public float FadeOutTime;
        public float ScaleX; // Fixed X scale for this particle
        public float ScaleY; // Fixed Y scale for this particle
        public int Layer; // Cloud layer (0-2)

        public SkyCloudParticle(Vector3 position, Vector3 velocity, float maxLife, float scale, float rotation, float rotationSpeed)
        {
            Reset(position, velocity, maxLife, scale, rotation, rotationSpeed);
        }

        public void Reset(Vector3 position, Vector3 velocity, float maxLife, float scale, float rotation, float rotationSpeed)
        {
            Position = position;
            Velocity = velocity;
            MaxLife = maxLife;
            Life = maxLife;
            Scale = scale;
            Rotation = rotation;
            RotationSpeed = rotationSpeed;
            Color = Color.White;
            Alpha = 0.0f; // Start invisible for fade-in
            FadeInTime = maxLife * 0.2f; // 20% of life for fade in
            FadeOutTime = maxLife * 0.3f; // 30% of life for fade out
            BaseAlpha = 0.3f + (scale / 1000f) * 0.1f; // Much lower base alpha to prevent white screen
            AlphaVariation = 0.0f;

            // Fixed deformation per particle (won't change during lifetime)
            var random = new Random(position.GetHashCode()); // Deterministic based on position
            ScaleX = scale * (0.8f + random.NextSingle() * 0.4f); // 80-120% of base scale
            ScaleY = scale * (0.8f + random.NextSingle() * 0.4f); // 80-120% of base scale

            // Assign to random layer (0, 1, or 2)
            Layer = random.Next(0, 3);
        }

        public void Update(GameTime gameTime, Vector3 gravity, Vector3 wind)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Movement
            Position += Velocity * delta * 1.5f;
            Position += wind * delta * 1.2f;

            // Turbulence
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            Vector3 turbulence = new Vector3(
                MathF.Sin(time * 0.15f + Position.X * 0.008f) * 0.2f,
                MathF.Cos(time * 0.12f + Position.Y * 0.008f) * 0.2f,
                MathF.Sin(time * 0.08f) * 0.1f
            );
            Position += turbulence * delta;

            // Rotation
            Rotation += RotationSpeed * delta * 0.2f;

            // Life and alpha
            Life -= delta;
            float lifeRatio = Life / MaxLife;
            float timeFromStart = MaxLife - Life;

            // Smooth fade transitions
            float fadeAlpha = 1.0f;
            if (timeFromStart < FadeInTime)
            {
                float fadeProgress = timeFromStart / FadeInTime;
                fadeAlpha = fadeProgress * fadeProgress * (3.0f - 2.0f * fadeProgress);
            }
            else if (Life < FadeOutTime)
            {
                float fadeProgress = Life / FadeOutTime;
                fadeAlpha = fadeProgress * fadeProgress * (3.0f - 2.0f * fadeProgress);
            }

            // Gentle alpha variation
            float uniqueOffset = Position.X * 0.0003f + Position.Y * 0.0002f;
            AlphaVariation = MathF.Sin(time * 0.01f + uniqueOffset) * 0.1f;

            Alpha = (BaseAlpha + AlphaVariation) * fadeAlpha;
            Alpha = MathHelper.Clamp(Alpha, 0.0f, 0.8f);

            // Scale growth
            Scale += delta * 0.03f;
        }

        public bool IsAlive => Life > 0;
    }

    public class SkyCloudSystem : WorldObject
    {
        private List<SkyCloudParticle> _particles = new List<SkyCloudParticle>();
        private Random _random = new Random();
        private float _emissionTimer;
        private float _emissionRate = 0.001f; // Slower emission
        private int _maxParticles = 4000; // More particles for wide pre-loading
        private Texture2D _cloudTexture;
        private Texture2D _cloudLightTexture;
        private Vector3 _wind = new Vector3(16.0f, 8.0f, 0);
        private VertexPositionColorTexture[] _vertices;
        private short[] _indices;
        private BasicEffect _effect;
        private bool _initialized = false;

        // Map coverage settings
        private Vector2 _mapCenter = new Vector2(1280, 1280);
        private float _mapRadius = 8000f; // Very large radius for wide pre-loading
        private Vector3[] _emissionPoints;

        // Cloud layer heights (constant for each layer)
        private static readonly float[] LayerHeights = { -100f, 0f, 150f };

        // Wind strength for each layer (higher = stronger wind)
        private static readonly Vector3[] LayerWinds = {
            new Vector3(20.0f, 10.0f, 0),   // Layer 0: Weak wind
            new Vector3(25.0f, 13.0f, 0),  // Layer 1: Medium wind
            new Vector3(35.0f, 20.0f, 0)   // Layer 2: Strong wind
        };

        public override async Task Load()
        {
            // Load cloud textures
            try
            {
                await TextureLoader.Instance.Prepare("Effect/clouds.jpg");
                await TextureLoader.Instance.Prepare("Effect/cloudLight.jpg");
                _cloudTexture = TextureLoader.Instance.GetTexture2D("Effect/clouds.jpg");
                _cloudLightTexture = TextureLoader.Instance.GetTexture2D("Effect/cloudLight.jpg");
            }
            catch (Exception ex)
            {
                // Fallback to default textures if cloud textures are not available
                _cloudTexture = GraphicsManager.Instance.Pixel;
                _cloudLightTexture = GraphicsManager.Instance.Pixel;
                System.Diagnostics.Debug.WriteLine($"Cloud texture loading failed: {ex.Message}");
            }

            // Initialize particle rendering
            _vertices = new VertexPositionColorTexture[_maxParticles * 4];
            _indices = new short[_maxParticles * 6];

            // Setup indices for quads
            for (int i = 0; i < _maxParticles; i++)
            {
                int vertexIndex = i * 4;
                int indexIndex = i * 6;

                _indices[indexIndex] = (short)vertexIndex;
                _indices[indexIndex + 1] = (short)(vertexIndex + 1);
                _indices[indexIndex + 2] = (short)(vertexIndex + 2);
                _indices[indexIndex + 3] = (short)vertexIndex;
                _indices[indexIndex + 4] = (short)(vertexIndex + 2);
                _indices[indexIndex + 5] = (short)(vertexIndex + 3);
            }

            // Create effect for particle rendering
            _effect = new BasicEffect(MuGame.Instance.GraphicsDevice)
            {
                VertexColorEnabled = true,
                TextureEnabled = true,
                World = Matrix.Identity,
                View = Matrix.Identity,
                Projection = Matrix.Identity
            };

            // Create multiple emission points across the map for uniform coverage
            SetupEmissionPoints();

            _initialized = true;
            await base.Load();
        }

        private void SetupEmissionPoints()
        {
            var points = new List<Vector3>();
            const int gridSize = 3;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    float offsetX = (x / (float)(gridSize - 1) - 0.5f) * 100f;
                    float offsetY = (y / (float)(gridSize - 1) - 0.5f) * 100f;

                    Vector3 relativePos = new Vector3(offsetX, offsetY, 30f);
                    points.Add(relativePos);
                }
            }

            _emissionPoints = points.ToArray();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (!_initialized)
            {
                System.Diagnostics.Debug.WriteLine("SkyCloudSystem not initialized yet");
                return;
            }

            Position = Camera.Instance.Position;
            _emissionTimer += (float)time.ElapsedGameTime.TotalSeconds;

            if (_emissionTimer >= _emissionRate && _particles.Count < _maxParticles)
            {
                EmitParticleFromRandomPoint();
                _emissionTimer = 0f;
            }

            UpdateParticles(time);
        }

        private void UpdateParticles(GameTime time)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var particle = _particles[i];

                int layerIndex = Array.IndexOf(LayerHeights, particle.Position.Z);
                Vector3 windForLayer = layerIndex >= 0 ? LayerWinds[layerIndex] : LayerWinds[0];

                particle.Update(time, Vector3.Zero, windForLayer);

                if (!_particles[i].IsAlive)
                {
                    _particles.RemoveAt(i);
                }
            }
        }

        private Vector3 GetPlayerPosition()
        {
            if (MuGame.Instance.ActiveScene is GameScene gameScene)
            {
                return gameScene.Hero.TargetPosition;
            }
            return Camera.Instance.Position;
        }

        private void EmitParticleFromRandomPoint()
        {
            Vector3 playerPos = GetPlayerPosition();
            Vector3 emitPosition = GenerateEmitPosition(playerPos);

            int layerIndex = Array.IndexOf(LayerHeights, emitPosition.Z);
            Vector3 baseWind = layerIndex >= 0 ? LayerWinds[layerIndex] : LayerWinds[0];
            Vector3 velocity = baseWind + new Vector3(
                (_random.NextSingle() - 0.5f) * 2.0f,
                (_random.NextSingle() - 0.5f) * 1.5f,
                (_random.NextSingle() - 0.5f) * 0.3f
            );

            float maxLife = _random.Next(2400, 4800) / 60f;
            float scale = GetScaleForLayer(emitPosition.Z);
            float rotation = _random.Next(0, 360) * MathHelper.ToRadians(1);
            float rotationSpeed = (_random.NextSingle() - 0.5f) * 0.1f;

            _particles.Add(new SkyCloudParticle(emitPosition, velocity, maxLife, scale, rotation, rotationSpeed));
        }

        private Vector3 GenerateEmitPosition(Vector3 playerPos)
        {
            Vector3 emitPosition;
            int attempts = 0;
            do
            {
                float layerHeight = LayerHeights[_random.Next(0, LayerHeights.Length)];
                const int gridSize = 1500;
                int gridX = _random.Next(-8, 9);
                int gridY = _random.Next(-8, 9);

                emitPosition = new Vector3(
                    playerPos.X + (gridX * gridSize) + (_random.NextSingle() - 0.5f) * 1000f,
                    playerPos.Y + (gridY * gridSize) + (_random.NextSingle() - 0.5f) * 1000f,
                    layerHeight
                );
                attempts++;
            } while (attempts < 20 && IsPositionTooClose(emitPosition, 800f));

            return emitPosition;
        }

        private float GetScaleForLayer(float layerHeight)
        {
            if (layerHeight <= -100f)
                return _random.NextSingle() * 400f + 800f;
            if (layerHeight <= 0f)
                return _random.NextSingle() * 200f + 400f;
            return _random.NextSingle() * 150f + 200f;
        }

        private bool IsPositionTooClose(Vector3 newPosition, float minDistance)
        {
            foreach (var particle in _particles)
            {
                float distance = Vector2.Distance(
                    new Vector2(particle.Position.X, particle.Position.Y),
                    new Vector2(newPosition.X, newPosition.Y)
                );

                float requiredDistance = CalculateRequiredDistance(particle.Position, newPosition, minDistance);

                if (distance < requiredDistance)
                    return true;
            }
            return false;
        }

        private float CalculateRequiredDistance(Vector3 existingPos, Vector3 newPos, float minDistance)
        {
            float requiredDistance = minDistance;

            if (Math.Abs(existingPos.Z - newPos.Z) < 50f)
            {
                requiredDistance = minDistance * 1.8f;
            }
            else
            {
                requiredDistance = minDistance * 0.6f;
            }

            if (existingPos.Z <= -100f || newPos.Z <= -100f)
            {
                requiredDistance *= 1.5f;
            }

            return requiredDistance;
        }

        public override void Draw(GameTime time)
        {
            if (_initialized && _particles.Count > 0)
            {
                DrawParticles(time);
            }
        }

        private void DrawParticles(GameTime time)
        {
            var graphicsDevice = MuGame.Instance.GraphicsDevice;
            var camera = Camera.Instance;

            // Setup render state - copied from CloudObject
            var oldBlendState = graphicsDevice.BlendState;
            var oldDepthStencilState = graphicsDevice.DepthStencilState;

            graphicsDevice.BlendState = BlendState.Additive; // Remove black background
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead; // Same as CloudObject

            // Update effect matrices
            _effect.World = Matrix.Identity;
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.Texture = _cloudTexture;

            // Build vertex buffer for all particles
            int particleCount = Math.Min(_particles.Count, _maxParticles);
            for (int i = 0; i < particleCount; i++)
            {
                var particle = _particles[i];
                CreateBillboard(particle, i);
            }

            if (particleCount > 0)
            {
                // Draw particles
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertices,
                        0,
                        particleCount * 4,
                        _indices,
                        0,
                        particleCount * 2
                    );
                }
            }

            // Restore render state
            graphicsDevice.BlendState = oldBlendState;
            graphicsDevice.DepthStencilState = oldDepthStencilState;
        }

        private void CreateBillboard(SkyCloudParticle particle, int index)
        {
            float cos = MathF.Cos(particle.Rotation);
            float sin = MathF.Sin(particle.Rotation);

            Vector3 right = new Vector3(cos * particle.ScaleX, sin * particle.ScaleX, 0);
            Vector3 up = new Vector3(-sin * particle.ScaleY, cos * particle.ScaleY, 0);

            int vertexIndex = index * 4;
            Color color = new Color(particle.Alpha, particle.Alpha, particle.Alpha, particle.Alpha);

            _vertices[vertexIndex] = new VertexPositionColorTexture(
                particle.Position - right - up, color, new Vector2(0, 0));
            _vertices[vertexIndex + 1] = new VertexPositionColorTexture(
                particle.Position + right - up, color, new Vector2(1, 0));
            _vertices[vertexIndex + 2] = new VertexPositionColorTexture(
                particle.Position + right + up, color, new Vector2(1, 1));
            _vertices[vertexIndex + 3] = new VertexPositionColorTexture(
                particle.Position - right + up, color, new Vector2(0, 1));
        }

        public override void Dispose()
        {
            _effect?.Dispose();
            base.Dispose();
        }
    }
}