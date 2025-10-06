using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Configuration;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public sealed class LorenciaLeafAmbientEffect : WorldObject
    {
        private readonly float _spawnOffsetX;
        private readonly float _spawnOffsetBack;
        private readonly float _spawnOffsetForward;
        private readonly float _spawnHeightMin;
        private readonly float _spawnHeightMax;

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
        private readonly float _maxDistanceSq;
        private readonly float _baseScale;
        private readonly float _scaleVariance;
        private readonly float _tiltStrength;
        private readonly float _swayStrength;
        private readonly Vector2 _windDirection;
        private readonly float _windVariance;
        private readonly float _windAlignment;
        private readonly float _windSpeedMultiplier;
        private readonly float _upwindSpawnDistance;
        private readonly float _initialFillRatio;
        private readonly string _texturePath;

        private readonly List<LeafParticle> _particles = new();
        private Texture2D _texture;
        private VertexPositionColorTexture[] _vertices;
        private short[] _indices;
        private float _spawnAccumulator;
        private float _time;
        private bool _needsInitialFill = true;

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
            public float BaseSpeed;
            public Vector2 PreferredDirection;
        }

        public LorenciaLeafAmbientEffect(WalkableWorldControl world, LorenciaLeafEffectSettings settings)
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
            float distance = MathF.Max(100f, settings.MaxDistance);
            _maxDistanceSq = distance * distance;
            _baseScale = MathF.Max(4f, settings.BaseScale);
            _scaleVariance = MathF.Max(0f, settings.ScaleVariance);
            _tiltStrength = MathF.Max(0f, settings.TiltStrength);
            _swayStrength = MathF.Max(0f, settings.SwayStrength);

            var texturePaths = settings.TexturePaths;
            _texturePath = texturePaths != null && texturePaths.Length > 0
                ? texturePaths[0]
                : settings.TexturePath ?? "World1/leaf01.tga";

            _spawnOffsetX = MathF.Max(0f, settings.SpawnPaddingX);
            _spawnOffsetBack = MathF.Max(0f, settings.SpawnPaddingBack);
            _spawnOffsetForward = MathF.Max(0f, settings.SpawnPaddingForward);
            _spawnHeightMin = MathF.Max(0f, settings.SpawnHeightMin);
            _spawnHeightMax = MathF.Max(_spawnHeightMin + 1f, settings.SpawnHeightMax);

            var windDirection = new Vector2(settings.WindDirectionX, settings.WindDirectionY);
            if (!float.IsFinite(windDirection.X) || !float.IsFinite(windDirection.Y) || windDirection.LengthSquared() < 0.0001f)
            {
                windDirection = new Vector2(6f, 14f);
            }
            windDirection.Normalize();
            _windDirection = windDirection;
            _windVariance = MathHelper.Clamp(settings.WindVariance, 0f, 2f);
            _windAlignment = MathHelper.Clamp(settings.WindAlignment, 0f, 5f);
            _windSpeedMultiplier = MathF.Max(0.05f, settings.WindSpeedMultiplier);
            _upwindSpawnDistance = MathF.Max(0f, settings.UpwindSpawnDistance);
            _initialFillRatio = MathHelper.Clamp(settings.InitialFillRatio, 0f, 1f);

            IsTransparent = true;
            float bboxHorizontal = _spawnOffsetX + _upwindSpawnDistance;
            float bboxBack = _spawnOffsetBack + _upwindSpawnDistance;
            float bboxForward = _spawnOffsetForward + _upwindSpawnDistance;
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-bboxHorizontal, -bboxBack, -(_spawnHeightMin + 200f)),
                new Vector3(bboxHorizontal, bboxForward, _spawnHeightMax + 200f));
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

                if (particle.Age >= particle.Lifetime)
                {
                    RemoveParticle(i);
                    continue;
                }

                Vector3 randomDrift = new Vector3(
                    RandomRange(-1f, 1f) * (_driftStrength * 0.35f),
                    RandomRange(-1f, 1f) * (_driftStrength * 0.35f),
                    RandomRange(-0.5f, 0.5f) * (_driftStrength * 0.6f));
                particle.Velocity += randomDrift * dt;

                Vector2 currentHorizontal = new Vector2(particle.Velocity.X, particle.Velocity.Y);
                Vector2 preferredDirection = NormalizeSafe(particle.PreferredDirection, _windDirection);
                Vector2 targetHorizontal = preferredDirection * particle.BaseSpeed;
                float alignFactor = MathHelper.Clamp(_windAlignment * dt, 0f, 1f);
                currentHorizontal = Vector2.Lerp(currentHorizontal, targetHorizontal, alignFactor);

                float currentSpeed = currentHorizontal.Length();
                float maxAllowedSpeed = _maxHorizontalSpeed * _windSpeedMultiplier;
                if (currentSpeed > maxAllowedSpeed && currentSpeed > 0f)
                {
                    currentHorizontal *= maxAllowedSpeed / currentSpeed;
                }

                particle.Velocity.X = currentHorizontal.X;
                particle.Velocity.Y = currentHorizontal.Y;

                if (_windVariance > 0f)
                {
                    Vector2 noise = new Vector2(RandomRange(-1f, 1f), RandomRange(-1f, 1f)) * (_windVariance * 0.05f);
                    preferredDirection = NormalizeSafe(preferredDirection + noise, preferredDirection);
                }

                float dirAlign = MathHelper.Clamp(_windAlignment * dt * 0.4f, 0f, 1f);
                Vector2 globalAlign = NormalizeSafe(Vector2.Lerp(preferredDirection, _windDirection, dirAlign), _windDirection);
                particle.PreferredDirection = globalAlign;

                float verticalLimit = _verticalSpeedRange;
                if (verticalLimit > 0f)
                {
                    particle.Velocity.Z = MathHelper.Clamp(particle.Velocity.Z, -verticalLimit, verticalLimit);
                }

                particle.Position += particle.Velocity * dt;

                if (_swayStrength > 0f)
                {
                    float sway = MathF.Sin((_time * particle.SwaySpeed) + particle.SwayPhase);
                    float lift = MathF.Cos((_time * particle.SwaySpeed * 0.7f) + particle.SwayPhase * 0.35f);
                    particle.Position += new Vector3(sway * (_swayStrength * dt * 0.5f), sway * (_swayStrength * dt * 0.2f), lift * (_swayStrength * dt * 0.15f));
                }

                particle.RollAngle += particle.RollSpeed * dt;
                if (particle.RollAngle > MathF.Tau)
                {
                    particle.RollAngle -= MathF.Tau;
                }
                else if (particle.RollAngle < 0f)
                {
                    particle.RollAngle += MathF.Tau;
                }

                Vector2 planarOffset = new Vector2(particle.Position.X - heroPosition.X, particle.Position.Y - heroPosition.Y);
                if (planarOffset.LengthSquared() > _maxDistanceSq)
                {
                    RemoveParticle(i);
                    continue;
                }

                _particles[i] = particle;
            }

            if (_spawnRate <= 0f)
            {
                return;
            }

            _spawnAccumulator += _spawnRate * dt;
            int spawnCount = Math.Min((int)_spawnAccumulator, _maxParticles - _particles.Count);
            if (spawnCount > 0)
            {
                _spawnAccumulator -= spawnCount;
                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnParticle(heroPosition);
                }
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

            Vector2 hero2D = new Vector2(heroPosition.X, heroPosition.Y);
            Vector2 spawn2D;

            bool spawnUpwind = _upwindSpawnDistance > 0f && MuGame.Random.NextDouble() < 0.75;
            if (spawnUpwind)
            {
                Vector2 baseWind = NormalizeSafe(_windDirection, new Vector2(1f, 0f));
                float distance = _upwindSpawnDistance + RandomRange(0f, _spawnOffsetForward * 0.5f);
                Vector2 perpendicular = new Vector2(-baseWind.Y, baseWind.X);
                spawn2D = hero2D - baseWind * distance;
                spawn2D += perpendicular * RandomRange(-_spawnOffsetX, _spawnOffsetX);
                spawn2D += baseWind * RandomRange(-_spawnOffsetBack, _spawnOffsetForward) * 0.25f;
            }
            else
            {
                spawn2D = hero2D + new Vector2(RandomRange(-_spawnOffsetX, _spawnOffsetX), RandomRange(-_spawnOffsetBack, _spawnOffsetForward));
            }

            float spawnX = spawn2D.X;
            float spawnY = spawn2D.Y;

            float terrainHeight = heroPosition.Z;
            if (_world?.Terrain != null)
            {
                terrainHeight = _world.Terrain.RequestTerrainHeight(spawnX, spawnY);
                if (!float.IsFinite(terrainHeight))
                {
                    terrainHeight = heroPosition.Z;
                }
            }

            float spawnZ = terrainHeight + RandomRange(_spawnHeightMin, _spawnHeightMax);
            Vector3 position = new Vector3(spawnX, spawnY, spawnZ);

            float speed = RandomRange(_minHorizontalSpeed, _maxHorizontalSpeed) * _windSpeedMultiplier;
            Vector2 direction = NormalizeSafe(_windDirection, new Vector2(1f, 0f));
            if (_windVariance > 0f)
            {
                Vector2 jitter = new Vector2(RandomRange(-1f, 1f), RandomRange(-1f, 1f)) * _windVariance;
                direction = NormalizeSafe(direction + jitter, direction);
            }

            Vector2 tangent = new Vector2(-direction.Y, direction.X);
            if (_windVariance > 0f)
            {
                float tangentScale = _windVariance * 0.05f;
                direction = NormalizeSafe(direction + tangent * RandomRange(-tangentScale, tangentScale), direction);
            }

            Vector3 velocity = new Vector3(direction.X, direction.Y, RandomRange(-_verticalSpeedRange, _verticalSpeedRange)) * speed;

            float scale = _baseScale + RandomRange(-_scaleVariance, _scaleVariance);
            scale = MathF.Max(4f, scale);

            var particle = new LeafParticle
            {
                Position = position,
                Velocity = velocity,
                Age = 0f,
                Lifetime = RandomRange(_minLifetime, _maxLifetime),
                Scale = scale,
                RollAngle = RandomRange(0f, MathF.Tau),
                RollSpeed = RandomRange(-0.9f, 0.9f),
                TiltPhase = RandomRange(0f, MathF.Tau),
                TiltSpeed = RandomRange(0.35f, 0.9f),
                SwayPhase = RandomRange(0f, MathF.Tau),
                SwaySpeed = RandomRange(0.4f, 1.0f),
                FadeIn = _fadeInDuration,
                FadeOut = _fadeOutDuration,
                BaseAlpha = MathHelper.Clamp(0.65f + RandomRange(-0.15f, 0.15f), 0.4f, 0.9f),
                BaseSpeed = speed,
                PreferredDirection = direction
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

            float tiltStrength = _tiltStrength;
            float swayStrength = _tiltStrength * 0.6f;

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
                if (tiltStrength > 0f)
                {
                    float tilt = tiltStrength * MathF.Sin((_time * particle.TiltSpeed) + particle.TiltPhase);
                    rotation *= Matrix.CreateFromAxisAngle(right, tilt);
                }
                if (swayStrength > 0f)
                {
                    float sway = swayStrength * MathF.Sin((_time * particle.TiltSpeed * 0.75f) + particle.TiltPhase * 1.3f);
                    rotation *= Matrix.CreateFromAxisAngle(up, sway);
                }

                right = Vector3.Normalize(Vector3.TransformNormal(right, rotation));
                up = Vector3.Normalize(Vector3.TransformNormal(up, rotation));
                forward = Vector3.Normalize(Vector3.Cross(right, up));
                right = Vector3.Normalize(Vector3.Cross(up, forward));

                float width = particle.Scale;
                float height = particle.Scale * 1.2f;
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

        private void RemoveParticle(int index)
        {
            int last = _particles.Count - 1;
            if (index < last)
            {
                _particles[index] = _particles[last];
            }
            _particles.RemoveAt(last);
        }

        private void PrefillParticles(Vector3 heroPosition)
        {
            int desired = Math.Min(_maxParticles, (int)MathF.Ceiling(_maxParticles * _initialFillRatio));
            for (int i = _particles.Count; i < desired; i++)
            {
                SpawnParticle(heroPosition);
            }
            _needsInitialFill = false;
        }

        private static Vector2 NormalizeSafe(Vector2 value, Vector2 fallback)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
                return fallback;

            float lengthSq = value.LengthSquared();
            if (lengthSq < 0.0001f)
                return fallback;

            float invLength = 1f / MathF.Sqrt(lengthSq);
            return value * invLength;
        }

        private static float RandomRange(float min, float max)
            => (float)(MuGame.Random.NextDouble() * (max - min) + min);
    }
}
