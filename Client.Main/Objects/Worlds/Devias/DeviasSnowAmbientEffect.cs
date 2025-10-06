using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Configuration;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Worlds.Devias
{
    public sealed class DeviasSnowAmbientEffect : WorldObject
    {
        private const float SpawnOffsetX = 850f;
        private const float SpawnOffsetBack = 600f;
        private const float SpawnOffsetForward = 1500f;
        private const float SpawnHeightMin = 220f;
        private const float SpawnHeightMax = 520f;
        private const float InitialFillRatio = 0.65f;

        private const int AlphaTestReference = 30;

        private readonly WalkableWorldControl _world;
        private readonly float _spawnRate;
        private readonly int _maxParticles;
        private readonly float _minLifetime;
        private readonly float _maxLifetime;
        private readonly float _fadeInDuration;
        private readonly float _fadeOutDuration;
        private readonly float _minHorizontalSpeed;
        private readonly float _maxHorizontalSpeed;
        private readonly float _verticalSpeedRange;
        private readonly float _driftStrength;
        private readonly float _gravity;
        private readonly float _groundFadeTime;
        private readonly float _maxDistanceSq;
        private readonly float _baseScale;
        private readonly float _scaleVariance;
        private readonly float _tiltStrength;
        private readonly float _swayStrength;
        private readonly Vector2 _windBias;

        private readonly string[] _texturePaths;
        private Texture2D[] _textures;
        private VertexPositionColorTexture[][] _verticesPerTexture;
        private short[][] _indicesPerTexture;
        private int[] _renderCounts;

        private readonly List<SnowParticle> _particles = new();
        private float _spawnAccumulator;
        private float _time;
        private bool _needsInitialFill = true;

        private struct SnowParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public float Scale;
            public float RollAngle;
            public float RollSpeed;
            public float TiltPhase;
            public float TiltSpeed;
            public float SwayPhase;
            public float SwaySpeed;
            public float FadeIn;
            public float FadeOut;
            public float BaseAlpha;
            public bool Grounded;
            public float GroundTimer;
            public byte TextureSlot;
        }

        public DeviasSnowAmbientEffect(WalkableWorldControl world, DeviasSnowEffectSettings settings)
        {
            _world = world;
            _spawnRate = MathF.Max(0f, settings.SpawnRate);
            _maxParticles = Math.Max(1, settings.MaxParticles);

            float minLifetime = MathF.Max(0.5f, settings.MinLifetime);
            float maxLifetime = MathF.Max(minLifetime, settings.MaxLifetime);
            _minLifetime = minLifetime;
            _maxLifetime = maxLifetime;

            _fadeInDuration = MathF.Max(0f, settings.FadeInDuration);
            _fadeOutDuration = MathF.Max(0f, settings.FadeOutDuration);
            _minHorizontalSpeed = MathF.Max(0f, MathF.Min(settings.MinHorizontalSpeed, settings.MaxHorizontalSpeed));
            _maxHorizontalSpeed = MathF.Max(_minHorizontalSpeed + 0.01f, MathF.Max(settings.MinHorizontalSpeed, settings.MaxHorizontalSpeed));
            _verticalSpeedRange = MathF.Max(10f, settings.VerticalSpeedRange);
            _driftStrength = MathF.Max(0f, settings.DriftStrength);
            _gravity = MathF.Max(0f, settings.Gravity);
            _groundFadeTime = MathF.Max(0.1f, settings.GroundFadeTime);
            float distance = MathF.Max(200f, settings.MaxDistance);
            _maxDistanceSq = distance * distance;
            _baseScale = MathF.Max(3f, settings.BaseScale);
            _scaleVariance = MathF.Max(0f, settings.ScaleVariance);
            _tiltStrength = MathF.Max(0f, settings.TiltStrength);
            _swayStrength = MathF.Max(0f, settings.SwayStrength);
            _windBias = new Vector2(settings.HorizontalBiasX, settings.HorizontalBiasY);

            var configuredPaths = settings.TexturePaths;
            if (configuredPaths != null && configuredPaths.Length > 0)
            {
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var list = new List<string>();
                foreach (var path in configuredPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path) && unique.Add(path))
                    {
                        list.Add(path);
                    }
                }
                if (list.Count == 0)
                {
                    list.Add(settings.TexturePath ?? "World3/leaf01.OZJ");
                }
                _texturePaths = list.ToArray();
            }
            else
            {
                var defaultList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { 
                    settings.TexturePath ?? "World3/leaf01.OZJ",
                    "World3/leaf02.OZJ"
                };
                _texturePaths = new List<string>(defaultList).ToArray();
            }

            IsTransparent = true;
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-SpawnOffsetX, -SpawnOffsetBack, -(SpawnHeightMin + 200f)),
                new Vector3(SpawnOffsetX, SpawnOffsetForward, SpawnHeightMax + 220f));
        }

        public async override Task LoadContent()
        {
            _textures = new Texture2D[_texturePaths.Length];
            _verticesPerTexture = new VertexPositionColorTexture[_texturePaths.Length][];
            _indicesPerTexture = new short[_texturePaths.Length][];
            _renderCounts = new int[_texturePaths.Length];

            for (int i = 0; i < _texturePaths.Length; i++)
            {
                string path = _texturePaths[i];
                await TextureLoader.Instance.Prepare(path);
                var rawTexture = TextureLoader.Instance.GetTexture2D(path) ?? GraphicsManager.Instance.Pixel;
                _textures[i] = CreateMaskedTexture(rawTexture);

                var vertices = new VertexPositionColorTexture[_maxParticles * 4];
                var indices = new short[_maxParticles * 6];
                for (int p = 0; p < _maxParticles; p++)
                {
                    int vOffset = p * 4;
                    int iOffset = p * 6;
                    indices[iOffset] = (short)vOffset;
                    indices[iOffset + 1] = (short)(vOffset + 1);
                    indices[iOffset + 2] = (short)(vOffset + 2);
                    indices[iOffset + 3] = (short)vOffset;
                    indices[iOffset + 4] = (short)(vOffset + 2);
                    indices[iOffset + 5] = (short)(vOffset + 3);
                }
                _verticesPerTexture[i] = vertices;
                _indicesPerTexture[i] = indices;
            }

            await base.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            var walker = _world?.Walker;
            if (walker != null)
            {
                Position = walker.Position;
            }

            base.Update(gameTime);
            if (Status != Client.Main.Models.GameControlStatus.Ready || walker == null)
            {
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
            {
                return;
            }

            _time += dt;
            Vector3 heroPosition = walker.Position;

            if (_needsInitialFill)
            {
                PrefillParticles(heroPosition);
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var particle = _particles[i];
                particle.Age += dt;

                if (!particle.Grounded && particle.Age >= particle.Lifetime)
                {
                    RemoveParticle(i);
                    continue;
                }

                if (!particle.Grounded)
                {
                    ApplyAirForces(ref particle, dt);
                }

                particle.Position += particle.Velocity * dt;

                float terrainHeight = RequestTerrainHeight(particle.Position.X, particle.Position.Y, heroPosition.Z);
                if (!particle.Grounded && particle.Position.Z <= terrainHeight)
                {
                    particle.Position.Z = terrainHeight;
                    particle.Velocity = Vector3.Zero;
                    particle.RollSpeed *= 0.4f;
                    particle.SwaySpeed *= 0.5f;
                    particle.TiltSpeed *= 0.5f;
                    particle.Grounded = true;
                    particle.GroundTimer = 0f;
                }

                if (particle.Grounded)
                {
                    particle.GroundTimer += dt;
                    if (particle.GroundTimer >= _groundFadeTime)
                    {
                        RemoveParticle(i);
                        continue;
                    }
                }

                Vector2 planarOffset = new Vector2(particle.Position.X - heroPosition.X, particle.Position.Y - heroPosition.Y);
                if (planarOffset.LengthSquared() > _maxDistanceSq)
                {
                    RemoveParticle(i);
                    continue;
                }

                _particles[i] = particle;
            }

            EmitNewParticles(heroPosition, dt);
        }

        private void ApplyAirForces(ref SnowParticle particle, float dt)
        {
            Vector3 randomDrift = new Vector3(
                RandomRange(-1f, 1f) * (_driftStrength * 0.3f),
                RandomRange(-1f, 1f) * (_driftStrength * 0.3f),
                RandomRange(-0.5f, 0.5f) * (_driftStrength * 0.2f));
            particle.Velocity += randomDrift * dt;

            particle.Velocity.X = MathHelper.Lerp(particle.Velocity.X, _windBias.X, 0.12f * dt);
            particle.Velocity.Y = MathHelper.Lerp(particle.Velocity.Y, _windBias.Y, 0.12f * dt);

            particle.Velocity.Z -= _gravity * dt;
            particle.Velocity.Z = MathHelper.Clamp(particle.Velocity.Z, -_verticalSpeedRange, _verticalSpeedRange * 0.3f);

            float sway = MathF.Sin((_time * particle.SwaySpeed) + particle.SwayPhase);
            float lift = MathF.Cos((_time * particle.SwaySpeed * 0.6f) + particle.SwayPhase * 0.42f);
            particle.Position.X += sway * (_swayStrength * dt * 0.35f);
            particle.Position.Y += sway * (_swayStrength * dt * 0.25f);
            particle.Position.Z += lift * (_swayStrength * dt * 0.18f);

            particle.RollAngle += particle.RollSpeed * dt * 0.6f;
            if (particle.RollAngle > MathF.Tau)
            {
                particle.RollAngle -= MathF.Tau;
            }
            else if (particle.RollAngle < 0f)
            {
                particle.RollAngle += MathF.Tau;
            }
        }

        private void EmitNewParticles(Vector3 heroPosition, float dt)
        {
            if (_spawnRate <= 0f || _particles.Count >= _maxParticles)
            {
                return;
            }

            _spawnAccumulator += _spawnRate * dt;
            int spawnCount = Math.Min((int)_spawnAccumulator, _maxParticles - _particles.Count);
            if (spawnCount <= 0)
            {
                return;
            }

            _spawnAccumulator -= spawnCount;
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnParticle(heroPosition);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (_textures == null || _textures.Length == 0 || _particles.Count == 0)
            {
                base.Draw(gameTime);
                return;
            }

            BuildGeometry();

            var device = GraphicsManager.Instance.GraphicsDevice;
            var alphaEffect = GraphicsManager.Instance.AlphaTestEffect3D;

            var previousBlend = device.BlendState;
            var previousDepth = device.DepthStencilState;
            var previousSampler = device.SamplerStates[0];
            var previousRasterizer = device.RasterizerState;
            var previousTexture = alphaEffect.Texture;
            int previousReferenceAlpha = alphaEffect.ReferenceAlpha;
            Matrix prevWorld = alphaEffect.World;
            Matrix prevView = alphaEffect.View;
            Matrix prevProj = alphaEffect.Projection;

            device.BlendState = BlendState.NonPremultiplied; // Alpha blending for snow particles
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.SamplerStates[0] = SamplerState.PointClamp;
            device.RasterizerState = RasterizerState.CullNone;

            alphaEffect.World = Matrix.Identity;
            alphaEffect.View = Camera.Instance.View;
            alphaEffect.Projection = Camera.Instance.Projection;
            alphaEffect.ReferenceAlpha = AlphaTestReference;

            for (int t = 0; t < _textures.Length; t++)
            {
                int count = _renderCounts[t];
                if (count == 0)
                    continue;

                alphaEffect.Texture = _textures[t];
                foreach (var pass in alphaEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _verticesPerTexture[t],
                        0,
                        count * 4,
                        _indicesPerTexture[t],
                        0,
                        count * 2);
                }
            }

            alphaEffect.Texture = previousTexture;
            alphaEffect.ReferenceAlpha = previousReferenceAlpha;
            alphaEffect.World = prevWorld;
            alphaEffect.View = prevView;
            alphaEffect.Projection = prevProj;
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.SamplerStates[0] = previousSampler;
            device.RasterizerState = previousRasterizer;

            base.Draw(gameTime);
        }

        public override void Dispose()
        {
            if (_textures != null)
            {
                for (int i = 0; i < _textures.Length; i++)
                {
                    if (_textures[i] != null && _textures[i] != GraphicsManager.Instance.Pixel)
                    {
                        _textures[i].Dispose();
                    }
                }
                _textures = null;
            }

            _particles.Clear();
            base.Dispose();
        }

        private void SpawnParticle(Vector3 heroPosition)
        {
            if (_particles.Count >= _maxParticles)
            {
                return;
            }

            Vector2 spawn2D = new Vector2(
                heroPosition.X + RandomRange(-SpawnOffsetX, SpawnOffsetX),
                heroPosition.Y + RandomRange(-SpawnOffsetBack, SpawnOffsetForward));

            float terrainHeight = RequestTerrainHeight(spawn2D.X, spawn2D.Y, heroPosition.Z);
            float spawnZ = terrainHeight + RandomRange(SpawnHeightMin, SpawnHeightMax);
            Vector3 position = new Vector3(spawn2D.X, spawn2D.Y, spawnZ);

            float speed = RandomRange(_minHorizontalSpeed, _maxHorizontalSpeed);
            float angle = RandomRange(0f, MathF.Tau);
            Vector3 velocity = new Vector3(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed, -RandomRange(_verticalSpeedRange * 0.25f, _verticalSpeedRange * 0.6f));

            float scale = _baseScale + RandomRange(-_scaleVariance, _scaleVariance);
            scale = MathF.Max(2.5f, scale);

            var particle = new SnowParticle
            {
                Position = position,
                Velocity = velocity,
                Age = 0f,
                Lifetime = RandomRange(_minLifetime, _maxLifetime),
                Scale = scale,
                RollAngle = RandomRange(0f, MathF.Tau),
                RollSpeed = RandomRange(-0.7f, 0.7f),
                TiltPhase = RandomRange(0f, MathF.Tau),
                TiltSpeed = RandomRange(0.25f, 0.55f),
                SwayPhase = RandomRange(0f, MathF.Tau),
                SwaySpeed = RandomRange(0.3f, 0.65f),
                FadeIn = _fadeInDuration,
                FadeOut = _fadeOutDuration,
                BaseAlpha = MathHelper.Clamp(0.9f + RandomRange(-0.05f, 0.05f), 0.75f, 1f),
                Grounded = false,
                GroundTimer = 0f,
                TextureSlot = (byte)(_textures != null && _textures.Length > 0 ? MuGame.Random.Next(_textures.Length) : 0)
            };

            _particles.Add(particle);
        }

        private void PrefillParticles(Vector3 heroPosition)
        {
            int desired = Math.Min(_maxParticles, (int)MathF.Ceiling(_maxParticles * InitialFillRatio));
            for (int i = _particles.Count; i < desired; i++)
            {
                SpawnParticle(heroPosition);
            }
            _needsInitialFill = false;
        }

        private void BuildGeometry()
        {
            if (_renderCounts == null)
                return;

            Array.Clear(_renderCounts, 0, _renderCounts.Length);

            Vector3 cameraPosition = Camera.Instance.Position;

            for (int i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                float alpha = ComputeAlpha(particle);
                if (alpha <= 0f)
                    continue;

                int slot = particle.TextureSlot % _textures.Length;
                int index = _renderCounts[slot];
                if (index >= _maxParticles)
                    continue;

                Vector3 forward = cameraPosition - particle.Position;
                if (forward.LengthSquared() < 0.0001f)
                {
                    forward = Vector3.UnitZ;
                }
                forward = Vector3.Normalize(forward);

                Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, forward));
                if (right.LengthSquared() < 0.0001f)
                {
                    right = Vector3.Normalize(Vector3.Cross(Vector3.UnitX, forward));
                }
                Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

                Matrix rotation = Matrix.Identity;
                rotation *= Matrix.CreateFromAxisAngle(forward, particle.RollAngle);
                if (!particle.Grounded && _tiltStrength > 0f)
                {
                    float tilt = _tiltStrength * MathF.Sin((_time * particle.TiltSpeed) + particle.TiltPhase);
                    rotation *= Matrix.CreateFromAxisAngle(right, tilt);
                }

                right = Vector3.Normalize(Vector3.TransformNormal(right, rotation));
                up = Vector3.Normalize(Vector3.TransformNormal(up, rotation));
                forward = Vector3.Normalize(Vector3.Cross(right, up));
                right = Vector3.Normalize(Vector3.Cross(up, forward));

                float width = particle.Scale;
                float height = particle.Scale * 1.1f;
                Vector3 rightVector = right * (width * 0.5f);
                Vector3 upVector = up * (height * 0.5f);

                Color color = new Color(1f, 1f, 1f, alpha);
                int vertexIndex = index * 4;
                var vertices = _verticesPerTexture[slot];

                vertices[vertexIndex] = new VertexPositionColorTexture(particle.Position - rightVector - upVector, color, new Vector2(0f, 1f));
                vertices[vertexIndex + 1] = new VertexPositionColorTexture(particle.Position + rightVector - upVector, color, new Vector2(1f, 1f));
                vertices[vertexIndex + 2] = new VertexPositionColorTexture(particle.Position + rightVector + upVector, color, new Vector2(1f, 0f));
                vertices[vertexIndex + 3] = new VertexPositionColorTexture(particle.Position - rightVector + upVector, color, new Vector2(0f, 0f));

                _renderCounts[slot] = index + 1;
            }
        }

        private float ComputeAlpha(in SnowParticle particle)
        {
            float alpha = particle.BaseAlpha;

            if (particle.Grounded)
            {
                float fade = MathHelper.Clamp(1f - (particle.GroundTimer / _groundFadeTime), 0f, 1f);
                return alpha * fade;
            }

            if (particle.FadeIn > 0f && particle.Age < particle.FadeIn)
            {
                float t = particle.Age / particle.FadeIn;
                alpha *= MathHelper.Clamp(t, 0f, 1f);
            }

            float remaining = particle.Lifetime - particle.Age;
            if (particle.FadeOut > 0f && remaining < particle.FadeOut)
            {
                float t = remaining / particle.FadeOut;
                alpha *= MathHelper.Clamp(t, 0f, 1f);
            }

            return MathHelper.Clamp(alpha, 0f, 1f);
        }

        private float RequestTerrainHeight(float x, float y, float fallback)
        {
            var terrain = _world?.Terrain;
            if (terrain != null)
            {
                float height = terrain.RequestTerrainHeight(x, y);
                if (float.IsFinite(height))
                {
                    return height;
                }
            }
            return fallback;
        }

        private void RemoveParticle(int index)
        {
            int last = _particles.Count - 1;
            if (index < last)
            {
                _particles[index] = _particles[last];
            }
            _particles.RemoveAt(last);
        }

        private static Texture2D CreateMaskedTexture(Texture2D source)
        {
            if (source == null)
                return GraphicsManager.Instance.Pixel;

            int width = source.Width;
            int height = source.Height;
            var device = GraphicsManager.Instance.GraphicsDevice;

            var pixels = new Color[width * height];
            source.GetData(pixels);

            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float luminance = (c.R + c.G + c.B) / 3f;

                // Use luminance as alpha - dark pixels (black background) will have alpha=0 and be discarded by AlphaTest
                // Bright pixels (white snowflakes) will have alpha=255 and be drawn
                byte intensity = (byte)MathHelper.Clamp(luminance, 0f, 255f);
                pixels[i] = new Color((byte)255, (byte)255, (byte)255, intensity); // White color with alpha from luminance
            }

            var masked = new Texture2D(device, width, height, false, SurfaceFormat.Color);
            masked.SetData(pixels);
            return masked;
        }

        private static float RandomRange(float min, float max)
            => (float)(MuGame.Random.NextDouble() * (max - min) + min);
    }
}
