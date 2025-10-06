using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Configuration;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Worlds.Noria
{
    public sealed class NoriaLeafAmbientEffect : WorldObject
    {
        private const float SpawnOffsetX = 800f;
        private const float SpawnOffsetBack = 500f;
        private const float SpawnOffsetForward = 1400f;
        private const float SpawnHeightMin = 200f;
        private const float SpawnHeightMax = 450f;
        private const float InitialFillRatio = 0.6f;
        private const float HorizontalSpeedMultiplier = 0.4f;

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
        private readonly string _texturePath;

        private readonly List<LeafParticle> _particles = new();
        private Texture2D _texture;
        private VertexPositionColorTexture[] _vertices;
        private short[] _indices;
        private float _spawnAccumulator;
        private bool _needsInitialFill = true;
        private float _time;

        private struct LeafParticle
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
        }

        public NoriaLeafAmbientEffect(WalkableWorldControl world, NoriaLeafEffectSettings settings)
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
            _verticalSpeedRange = MathF.Max(0f, settings.VerticalSpeedRange);
            _driftStrength = MathF.Max(0f, settings.DriftStrength);
            _gravity = MathF.Max(0f, settings.Gravity);
            _groundFadeTime = MathF.Max(0.1f, settings.GroundFadeTime);
            float distance = MathF.Max(100f, settings.MaxDistance);
            _maxDistanceSq = distance * distance;
            _baseScale = MathF.Max(3f, settings.BaseScale);
            _scaleVariance = MathF.Max(0f, settings.ScaleVariance);
            _tiltStrength = MathF.Max(0f, settings.TiltStrength);
            _swayStrength = MathF.Max(0f, settings.SwayStrength);

            var texturePaths = settings.TexturePaths;
            _texturePath = texturePaths != null && texturePaths.Length > 0
                ? texturePaths[MuGame.Random.Next(texturePaths.Length)]
                : settings.TexturePath ?? "World4/leaf01.tga";

            IsTransparent = true;
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-SpawnOffsetX, -SpawnOffsetBack, -(SpawnHeightMin + 200f)),
                new Vector3(SpawnOffsetX, SpawnOffsetForward, SpawnHeightMax + 200f));
        }

        public async override Task LoadContent()
        {
            await TextureLoader.Instance.Prepare(_texturePath);
            _texture = TextureLoader.Instance.GetTexture2D(_texturePath) ?? GraphicsManager.Instance.Pixel;

            _vertices = new VertexPositionColorTexture[_maxParticles * 4];
            _indices = new short[_maxParticles * 6];

            for (int i = 0; i < _maxParticles; i++)
            {
                int v = i * 4;
                int idx = i * 6;
                _indices[idx] = (short)v;
                _indices[idx + 1] = (short)(v + 1);
                _indices[idx + 2] = (short)(v + 2);
                _indices[idx + 3] = (short)v;
                _indices[idx + 4] = (short)(v + 2);
                _indices[idx + 5] = (short)(v + 3);
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
                    particle.RollSpeed = 0f;
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

        private void ApplyAirForces(ref LeafParticle particle, float dt)
        {
            Vector3 randomDrift = new Vector3(
                RandomRange(-1f, 1f) * (_driftStrength * 0.25f),
                RandomRange(-1f, 1f) * (_driftStrength * 0.25f),
                RandomRange(-0.5f, 0.5f) * (_driftStrength * 0.18f));
            particle.Velocity += randomDrift * dt;

            particle.Velocity.X = MathHelper.Lerp(particle.Velocity.X, 0f, 0.2f * dt);
            particle.Velocity.Y = MathHelper.Lerp(particle.Velocity.Y, 0f, 0.2f * dt);

            particle.Velocity.Z -= _gravity * dt;
            float maxFallSpeed = _verticalSpeedRange;
            particle.Velocity.Z = MathHelper.Clamp(particle.Velocity.Z, -maxFallSpeed, maxFallSpeed * 0.25f);

            float sway = MathF.Sin((_time * particle.SwaySpeed) + particle.SwayPhase);
            float lift = MathF.Cos((_time * particle.SwaySpeed * 0.7f) + particle.SwayPhase * 0.35f);
            particle.Position.X += sway * (_swayStrength * dt * 0.45f);
            particle.Position.Y += sway * (_swayStrength * dt * 0.35f);
            particle.Position.Z += lift * (_swayStrength * dt * 0.22f);

            Vector2 softWind = new Vector2(18f, -12f);
            particle.Velocity.X += softWind.X * dt * 0.15f;
            particle.Velocity.Y += softWind.Y * dt * 0.15f;

            particle.RollAngle += particle.RollSpeed * dt * 0.4f;
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
            if (_texture == null || _particles.Count == 0)
            {
                base.Draw(gameTime);
                return;
            }

            int renderCount = BuildGeometry();
            if (renderCount == 0)
            {
                base.Draw(gameTime);
                return;
            }

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

            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.SamplerStates[0] = SamplerState.PointClamp;
            device.RasterizerState = RasterizerState.CullNone;

            alphaEffect.Texture = _texture;
            alphaEffect.ReferenceAlpha = 40;
            alphaEffect.World = Matrix.Identity;
            alphaEffect.View = Camera.Instance.View;
            alphaEffect.Projection = Camera.Instance.Projection;

            foreach (var pass in alphaEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices,
                    0,
                    renderCount * 4,
                    _indices,
                    0,
                    renderCount * 2);
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

            float speed = RandomRange(_minHorizontalSpeed, _maxHorizontalSpeed) * HorizontalSpeedMultiplier;
            float angle = RandomRange(0f, MathF.Tau);
            Vector3 velocity = new Vector3(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed, 0f);
            velocity.Z = -RandomRange(_verticalSpeedRange * 0.2f, _verticalSpeedRange * 0.45f);

            float scale = _baseScale + RandomRange(-_scaleVariance, _scaleVariance);
            scale = MathF.Max(3f, scale);

            var particle = new LeafParticle
            {
                Position = position,
                Velocity = velocity,
                Age = 0f,
                Lifetime = RandomRange(_minLifetime, _maxLifetime),
                Scale = scale,
                RollAngle = RandomRange(0f, MathF.Tau),
                RollSpeed = RandomRange(-0.6f, 0.6f),
                TiltPhase = RandomRange(0f, MathF.Tau),
                TiltSpeed = RandomRange(0.2f, 0.6f),
                SwayPhase = RandomRange(0f, MathF.Tau),
                SwaySpeed = RandomRange(0.25f, 0.6f),
                FadeIn = _fadeInDuration,
                FadeOut = _fadeOutDuration,
                BaseAlpha = MathHelper.Clamp(0.55f + RandomRange(-0.1f, 0.1f), 0.35f, 0.8f),
                Grounded = false,
                GroundTimer = 0f
            };

            _particles.Add(particle);
        }

        private int BuildGeometry()
        {
            var camera = Camera.Instance;
            if (camera == null)
            {
                return 0;
            }

            Vector3 cameraPosition = camera.Position;
            int renderIndex = 0;

            for (int i = 0; i < _particles.Count && renderIndex < _maxParticles; i++)
            {
                var particle = _particles[i];
                float alpha = ComputeAlpha(particle);
                if (alpha <= 0f)
                {
                    continue;
                }

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
                float height = particle.Scale * 1.15f;
                Vector3 rightVector = right * (width * 0.5f);
                Vector3 upVector = up * (height * 0.5f);

                Color color = new Color(1f, 1f, 1f, alpha);
                int vertexIndex = renderIndex * 4;

                _vertices[vertexIndex] = new VertexPositionColorTexture(particle.Position - rightVector - upVector, color, new Vector2(0f, 1f));
                _vertices[vertexIndex + 1] = new VertexPositionColorTexture(particle.Position + rightVector - upVector, color, new Vector2(1f, 1f));
                _vertices[vertexIndex + 2] = new VertexPositionColorTexture(particle.Position + rightVector + upVector, color, new Vector2(1f, 0f));
                _vertices[vertexIndex + 3] = new VertexPositionColorTexture(particle.Position - rightVector + upVector, color, new Vector2(0f, 0f));

                renderIndex++;
            }

            return renderIndex;
        }

        private float ComputeAlpha(in LeafParticle particle)
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

        private void PrefillParticles(Vector3 heroPosition)
        {
            int desired = Math.Min(_maxParticles, (int)MathF.Ceiling(_maxParticles * InitialFillRatio));
            for (int i = _particles.Count; i < desired; i++)
            {
                SpawnParticle(heroPosition);
            }
            _needsInitialFill = false;
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

        private static float RandomRange(float min, float max)
            => (float)(MuGame.Random.NextDouble() * (max - min) + min);
    }
}
