#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Flame (Scroll of Flame) effect - cylindrical fire wall with volumetric flames.
    /// Combines cylinder structure with organic rising fire particles.
    /// </summary>
    public sealed class ScrollOfFlameEffect : EffectObject
    {
        private const ushort FlameSkillId = 5;

        private const string FlameTexturePath = "Effect/Flame01.jpg";
        private const string FlameFallbackTexturePath = "Effect/firehik01.jpg";
        private const string SparkTexturePath = "Effect/Spark03.jpg";
        private const string GlowTexturePath = "Effect/flare.jpg";

        // Timing
        private const float AreaDurationSeconds = 2.2f;
        private const float TargetedDurationSeconds = 1.4f;
        private const float FadeOutSeconds = 0.5f;

        // Cylinder parameters - compact fire pillar
        private const float CylinderRadius = 35f;
        private const float CylinderHeight = 450f;
        private const float CylinderThickness = 10f; // very tight spawn area

        // Damage (matches original client behavior: hits in radius ~150 every ~20 frames @ 25fps)
        private const float DamageRadius = 150f;
        private const float DamageTickSeconds = 20f / 25f;
        private const int MaxHitTargets = 5;
        private const double LastCastMatchWindowMs = 1500;

        // Flame particles - dense fire column
        private const int MaxFlameParticles = 350;
        private const float FlameSpawnRate = 280f; // per second - very dense
        private const float FlameLifetimeMin = 0.55f;
        private const float FlameLifetimeMax = 0.95f;
        private const float FlameRiseSpeedMin = 420f;
        private const float FlameRiseSpeedMax = 620f;
        private const float FlameSizeMin = 28f;
        private const float FlameSizeMax = 55f;
        private const float FlameAcceleration = 280f;

        // Ground flames
        private const int GroundFlameCount = 10;
        private const float GroundFlameSize = 50f;

        // Sparks
        private const int MaxSparks = 48;
        private const float SparkSpawnRate = 35f;
        private const float SparkLifetimeMin = 0.35f;
        private const float SparkLifetimeMax = 0.75f;
        private const float SparkRiseSpeed = 320f;
        private const float SparkSizeMin = 16f;
        private const float SparkSizeMax = 36f;

        private readonly Vector3 _center;
        private readonly bool _isTargeted;
        private readonly bool _dealsDamage;
        private readonly float _totalDuration;
        private float _time;

        private readonly byte _targetTileX;
        private readonly byte _targetTileY;
        private byte _animationCounter;
        private byte _hitCounter;
        private readonly System.Collections.Generic.Dictionary<ushort, float> _nextHitTimeByTarget = new();
        private readonly ILogger? _logger = MuGame.AppLoggerFactory?.CreateLogger<ScrollOfFlameEffect>();

        private Texture2D _flameTexture = null!;
        private Texture2D _sparkTexture = null!;
        private Texture2D _glowTexture = null!;

        // Particles
        private readonly FlameParticle[] _flames = new FlameParticle[MaxFlameParticles];
        private int _flameCount;
        private float _flameSpawnTimer;

        private readonly FlameParticle[] _sparks = new FlameParticle[MaxSparks];
        private int _sparkCount;
        private float _sparkSpawnTimer;

        // Ground flames (stationary, arranged in circle)
        private readonly GroundFlame[] _groundFlames = new GroundFlame[GroundFlameCount];

        // Vertex buffers
        private const int MaxQuads = MaxFlameParticles + MaxSparks + GroundFlameCount + 1; // +1 for central glow
        private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[MaxQuads * 4];
        private readonly short[] _indices = new short[MaxQuads * 6];

        private readonly DynamicLight _flameLight;
        private readonly DynamicLight _topLight;
        private bool _lightsAdded;

        private struct FlameParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public float HeightScale; // for elongated flames
        }

        private struct GroundFlame
        {
            public Vector3 Position;
            public float Angle;
            public float Phase; // for animation offset
        }

        public ScrollOfFlameEffect(Vector3 center, bool isTargeted = false, bool dealsDamage = false)
        {
            _center = center;
            _isTargeted = isTargeted;
            _dealsDamage = dealsDamage;
            _totalDuration = isTargeted ? TargetedDurationSeconds : AreaDurationSeconds;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;

            _targetTileX = (byte)Math.Clamp((int)(_center.X / Constants.TERRAIN_SCALE), 0, Constants.TERRAIN_SIZE - 1);
            _targetTileY = (byte)Math.Clamp((int)(_center.Y / Constants.TERRAIN_SCALE), 0, Constants.TERRAIN_SIZE - 1);

            float boundSize = CylinderRadius + 120f;
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-boundSize, -boundSize, -20f),
                new Vector3(boundSize, boundSize, CylinderHeight + 200f));

            _flameLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.35f, 0.08f), // red light
                Radius = 280f,
                Intensity = 2.0f
            };

            _topLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.4f, 0.1f), // red light
                Radius = 180f,
                Intensity = 1.2f
            };

            Position = _center;

            InitializeIndices();
            InitializeGroundFlames();
        }

        private void InitializeIndices()
        {
            for (int i = 0; i < MaxQuads; i++)
            {
                int vi = i * 4;
                int ii = i * 6;
                _indices[ii] = (short)vi;
                _indices[ii + 1] = (short)(vi + 1);
                _indices[ii + 2] = (short)(vi + 2);
                _indices[ii + 3] = (short)vi;
                _indices[ii + 4] = (short)(vi + 2);
                _indices[ii + 5] = (short)(vi + 3);
            }
        }

        private void InitializeGroundFlames()
        {
            for (int i = 0; i < GroundFlameCount; i++)
            {
                float angle = (i / (float)GroundFlameCount) * MathHelper.TwoPi;
                float radius = CylinderRadius * RandomRange(0.85f, 1.05f);
                _groundFlames[i] = new GroundFlame
                {
                    Position = _center + new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 8f),
                    Angle = angle,
                    Phase = RandomRange(0f, MathHelper.TwoPi)
                };
            }
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(FlameTexturePath);
            _ = await TextureLoader.Instance.Prepare(FlameFallbackTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);
            _ = await TextureLoader.Instance.Prepare(GlowTexturePath);

            _flameTexture = TextureLoader.Instance.GetTexture2D(FlameTexturePath)
                ?? TextureLoader.Instance.GetTexture2D(FlameFallbackTexturePath)
                ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkTexturePath) ?? GraphicsManager.Instance.Pixel;
            _glowTexture = TextureLoader.Instance.GetTexture2D(GlowTexturePath) ?? GraphicsManager.Instance.Pixel;

            if (World?.Terrain != null && !_lightsAdded)
            {
                World.Terrain.AddDynamicLight(_flameLight);
                World.Terrain.AddDynamicLight(_topLight);
                _lightsAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
                _ = Load();

            if (Status != GameControlStatus.Ready)
                return;

            ForceInView();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;

            UpdateDamage(dt);

            bool isEmitting = _time < _totalDuration - FadeOutSeconds;

            if (isEmitting)
            {
                SpawnFlames(dt);
                SpawnSparks(dt);
            }

            UpdateFlames(dt);
            UpdateSparks(dt);
            UpdateDynamicLights();

            if (_time >= _totalDuration && _flameCount == 0 && _sparkCount == 0)
            {
                RemoveSelf();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _flameTexture == null)
                return;

            DrawEffect();
        }

        private void DrawEffect()
        {
            var gd = GraphicsManager.Instance.GraphicsDevice;
            var effect = GraphicsManager.Instance.BasicEffect3D;
            var camera = Camera.Instance;
            if (camera == null || effect == null)
                return;

            // Save state
            var prevBlend = gd.BlendState;
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevSampler = gd.SamplerStates[0];

            bool prevTexEnabled = effect.TextureEnabled;
            bool prevVCEnabled = effect.VertexColorEnabled;
            bool prevLightEnabled = effect.LightingEnabled;
            var prevTex = effect.Texture;
            Matrix prevWorld = effect.World;
            Matrix prevView = effect.View;
            Matrix prevProj = effect.Projection;

            gd.BlendState = BlendState.Additive;
            gd.DepthStencilState = DepthState;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            effect.TextureEnabled = true;
            effect.VertexColorEnabled = true;
            effect.LightingEnabled = false;
            effect.World = Matrix.Identity;
            effect.View = camera.View;
            effect.Projection = camera.Projection;

            float effectAlpha = GetEffectAlpha();
            int quadIndex = 0;

            // Build all flame billboards (ground flames + rising flames)
            BuildGroundFlames(camera, effectAlpha, ref quadIndex);
            BuildFlameParticles(camera, effectAlpha, ref quadIndex);
            int flameQuadCount = quadIndex;

            // Draw flames
            if (flameQuadCount > 0)
            {
                effect.Texture = _flameTexture;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertices, 0, flameQuadCount * 4,
                        _indices, 0, flameQuadCount * 2);
                }
            }

            // Build and draw sparks
            int sparkStartQuad = quadIndex;
            BuildSparks(camera, effectAlpha, ref quadIndex);
            int sparkQuadCount = quadIndex - sparkStartQuad;

            if (sparkQuadCount > 0)
            {
                effect.Texture = _sparkTexture;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertices, sparkStartQuad * 4, sparkQuadCount * 4,
                        _indices, sparkStartQuad * 6, sparkQuadCount * 2);
                }
            }

            // Build and draw central glow
            int glowStartQuad = quadIndex;
            BuildCentralGlow(camera, effectAlpha, ref quadIndex);
            int glowQuadCount = quadIndex - glowStartQuad;

            if (glowQuadCount > 0)
            {
                effect.Texture = _glowTexture;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertices, glowStartQuad * 4, glowQuadCount * 4,
                        _indices, glowStartQuad * 6, glowQuadCount * 2);
                }
            }

            // Restore state
            effect.TextureEnabled = prevTexEnabled;
            effect.VertexColorEnabled = prevVCEnabled;
            effect.LightingEnabled = prevLightEnabled;
            effect.Texture = prevTex;
            effect.World = prevWorld;
            effect.View = prevView;
            effect.Projection = prevProj;

            gd.BlendState = prevBlend;
            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.SamplerStates[0] = prevSampler;
        }

        private void SpawnFlames(float dt)
        {
            _flameSpawnTimer += dt;
            float spawnInterval = 1f / FlameSpawnRate;

            while (_flameSpawnTimer >= spawnInterval && _flameCount < MaxFlameParticles)
            {
                _flameSpawnTimer -= spawnInterval;
                SpawnFlameParticle();
            }
        }

        private void SpawnFlameParticle()
        {
            if (_flameCount >= MaxFlameParticles)
                return;

            // Spawn in cylinder wall area (ring with thickness)
            float angle = RandomRange(0f, MathHelper.TwoPi);
            float radiusOffset = RandomRange(-CylinderThickness * 0.5f, CylinderThickness * 0.5f);
            float radius = CylinderRadius + radiusOffset;

            // Start near ground
            float startHeight = RandomRange(0f, 25f);

            Vector3 pos = _center + new Vector3(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius,
                startHeight);

            // Velocity: mostly upward, almost no drift for very tight formation
            float riseSpeed = RandomRange(FlameRiseSpeedMin, FlameRiseSpeedMax);
            float drift = RandomRange(-3f, 3f); // minimal drift
            Vector3 outward = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);

            _flames[_flameCount++] = new FlameParticle
            {
                Position = pos,
                Velocity = new Vector3(outward.X * drift, outward.Y * drift, riseSpeed),
                Age = 0f,
                Lifetime = RandomRange(FlameLifetimeMin, FlameLifetimeMax),
                Size = RandomRange(FlameSizeMin, FlameSizeMax),
                Rotation = RandomRange(0f, MathHelper.TwoPi),
                RotationSpeed = RandomRange(-2f, 2f),
                HeightScale = RandomRange(1.5f, 2.5f) // flames are taller than wide
            };
        }

        private void UpdateFlames(float dt)
        {
            int i = 0;
            while (i < _flameCount)
            {
                ref var flame = ref _flames[i];
                flame.Age += dt;

                if (flame.Age >= flame.Lifetime)
                {
                    _flames[i] = _flames[--_flameCount];
                    continue;
                }

                // Rise with acceleration
                flame.Velocity.Z += FlameAcceleration * dt;
                flame.Position += flame.Velocity * dt;

                // Slow horizontal drift
                flame.Velocity.X *= 0.985f;
                flame.Velocity.Y *= 0.985f;

                // Rotate
                flame.Rotation += flame.RotationSpeed * dt;

                // Grow slightly as rising
                float lifeProgress = flame.Age / flame.Lifetime;
                if (lifeProgress > 0.5f)
                {
                    flame.Size *= 1f + dt * 0.3f; // expand near end
                }

                i++;
            }
        }

        private void SpawnSparks(float dt)
        {
            _sparkSpawnTimer += dt;
            float spawnInterval = 1f / SparkSpawnRate;

            while (_sparkSpawnTimer >= spawnInterval && _sparkCount < MaxSparks)
            {
                _sparkSpawnTimer -= spawnInterval;
                SpawnSparkParticle();
            }
        }

        private void SpawnSparkParticle()
        {
            if (_sparkCount >= MaxSparks)
                return;

            float angle = RandomRange(0f, MathHelper.TwoPi);
            float radius = CylinderRadius * RandomRange(0.8f, 1.1f);
            float height = RandomRange(CylinderHeight * 0.15f, CylinderHeight * 0.6f);

            Vector3 pos = _center + new Vector3(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius,
                height);

            Vector3 outward = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
            float outSpeed = RandomRange(20f, 50f); // less outward spread

            _sparks[_sparkCount++] = new FlameParticle
            {
                Position = pos,
                Velocity = new Vector3(
                    outward.X * outSpeed,
                    outward.Y * outSpeed,
                    SparkRiseSpeed * RandomRange(0.8f, 1.2f)),
                Age = 0f,
                Lifetime = RandomRange(SparkLifetimeMin, SparkLifetimeMax),
                Size = RandomRange(SparkSizeMin, SparkSizeMax),
                Rotation = RandomRange(0f, MathHelper.TwoPi),
                RotationSpeed = RandomRange(3f, 8f),
                HeightScale = 1f
            };
        }

        private void UpdateSparks(float dt)
        {
            int i = 0;
            while (i < _sparkCount)
            {
                ref var spark = ref _sparks[i];
                spark.Age += dt;

                if (spark.Age >= spark.Lifetime)
                {
                    _sparks[i] = _sparks[--_sparkCount];
                    continue;
                }

                spark.Position += spark.Velocity * dt;
                spark.Velocity.Z += 80f * dt; // upward acceleration
                spark.Velocity.X *= 0.97f;
                spark.Velocity.Y *= 0.97f;
                spark.Rotation += spark.RotationSpeed * dt;

                i++;
            }
        }

        private void BuildGroundFlames(Camera camera, float effectAlpha, ref int quadIndex)
        {
            Vector3 camPos = camera.Position;

            for (int i = 0; i < GroundFlameCount; i++)
            {
                ref var gf = ref _groundFlames[i];

                // Animated size and intensity
                float pulse = 0.7f + 0.3f * MathF.Sin(_time * 8f + gf.Phase);
                float size = GroundFlameSize * pulse;

                // Color with flicker - more red/orange
                float flicker = 0.85f + 0.15f * MathF.Sin(_time * 15f + gf.Phase * 2f);
                float alpha = effectAlpha * flicker;
                var color = new Color(alpha, alpha * 0.4f, alpha * 0.08f, alpha);

                // Billboard facing camera
                BuildBillboard(gf.Position, camPos, size, size * 2.2f, color, _time * 1.5f + gf.Phase, ref quadIndex);
            }
        }

        private void BuildFlameParticles(Camera camera, float effectAlpha, ref int quadIndex)
        {
            Vector3 camPos = camera.Position;

            for (int i = 0; i < _flameCount; i++)
            {
                ref var flame = ref _flames[i];

                float life = 1f - (flame.Age / flame.Lifetime);
                if (life <= 0.01f)
                    continue;

                // Fade in quickly, fade out slowly
                float fadeIn = MathHelper.Clamp(flame.Age / 0.1f, 0f, 1f);
                float fadeOut = life;
                float alpha = fadeIn * fadeOut * effectAlpha;

                // Color shifts from orange at base to deep red at top - more red overall
                float heightRatio = MathHelper.Clamp((flame.Position.Z - _center.Z) / CylinderHeight, 0f, 1f);
                float r = 1f;
                float g = MathHelper.Lerp(0.45f, 0.15f, heightRatio); // less green = more red
                float b = MathHelper.Lerp(0.12f, 0.02f, heightRatio); // minimal blue

                var color = new Color(r * alpha, g * alpha, b * alpha, alpha);

                float width = flame.Size * (0.8f + 0.2f * life);
                float height = width * flame.HeightScale;

                BuildBillboard(flame.Position, camPos, width, height, color, flame.Rotation, ref quadIndex);
            }
        }

        private void BuildSparks(Camera camera, float effectAlpha, ref int quadIndex)
        {
            Vector3 camPos = camera.Position;

            for (int i = 0; i < _sparkCount; i++)
            {
                ref var spark = ref _sparks[i];

                float life = 1f - (spark.Age / spark.Lifetime);
                if (life <= 0.01f)
                    continue;

                float alpha = life * effectAlpha;
                var color = new Color(alpha, alpha * 0.5f, alpha * 0.1f, alpha); // more red sparks

                float size = spark.Size * (0.6f + 0.4f * life);
                BuildBillboard(spark.Position, camPos, size, size, color, spark.Rotation, ref quadIndex);
            }
        }

        private void BuildCentralGlow(Camera camera, float effectAlpha, ref int quadIndex)
        {
            float pulse = 0.6f + 0.4f * MathF.Sin(_time * 6f);
            float alpha = effectAlpha * pulse * 0.55f;
            var color = new Color(alpha, alpha * 0.35f, alpha * 0.08f, alpha); // more red glow

            Vector3 glowPos = _center + new Vector3(0f, 0f, CylinderHeight * 0.3f);
            float glowSize = CylinderRadius * 2.5f; // adjusted for smaller radius

            BuildBillboard(glowPos, camera.Position, glowSize, glowSize, color, _time * 0.5f, ref quadIndex);
        }

        private void BuildBillboard(Vector3 position, Vector3 camPos, float width, float height, Color color, float rotation, ref int quadIndex)
        {
            if (quadIndex >= MaxQuads)
                return;

            Vector3 toCamera = camPos - position;
            if (toCamera.LengthSquared() < 0.001f)
                toCamera = Vector3.UnitY;
            toCamera.Normalize();

            Vector3 right = Vector3.Cross(Vector3.UnitZ, toCamera);
            if (right.LengthSquared() < 0.001f)
                right = Vector3.UnitX;
            right.Normalize();

            Vector3 up = Vector3.Cross(toCamera, right);
            up.Normalize();

            // Apply rotation
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);
            Vector3 rotRight = right * cos + up * sin;
            Vector3 rotUp = up * cos - right * sin;

            Vector3 r = rotRight * (width * 0.5f);
            Vector3 u = rotUp * (height * 0.5f);

            int vi = quadIndex * 4;
            _vertices[vi] = new VertexPositionColorTexture(position - r - u, color, new Vector2(0f, 1f));
            _vertices[vi + 1] = new VertexPositionColorTexture(position + r - u, color, new Vector2(1f, 1f));
            _vertices[vi + 2] = new VertexPositionColorTexture(position + r + u, color, new Vector2(1f, 0f));
            _vertices[vi + 3] = new VertexPositionColorTexture(position - r + u, color, new Vector2(0f, 0f));

            quadIndex++;
        }

        private void UpdateDynamicLights()
        {
            if (World?.Terrain == null)
                return;

            float alpha = GetEffectAlpha();
            float flicker = 0.8f + 0.2f * MathF.Sin(_time * 22f);

            _flameLight.Position = _center + new Vector3(0f, 0f, CylinderHeight * 0.25f);
            _flameLight.Intensity = 1.8f * alpha * flicker;
            _flameLight.Radius = 240f + 30f * MathF.Sin(_time * 7f);

            _topLight.Position = _center + new Vector3(0f, 0f, CylinderHeight * 0.7f);
            _topLight.Intensity = 1.0f * alpha * flicker;
            _topLight.Radius = 160f;
        }

        private float GetEffectAlpha()
        {
            if (_time >= _totalDuration)
                return 0f;

            // Fast fade in
            if (_time < 0.12f)
                return _time / 0.12f;

            // Fade out
            float fadeStart = _totalDuration - FadeOutSeconds;
            if (_time > fadeStart)
                return 1f - ((_time - fadeStart) / FadeOutSeconds);

            return 1f;
        }

        private static float RandomRange(float min, float max)
        {
            return (float)(MuGame.Random.NextDouble() * (max - min) + min);
        }

        private void UpdateDamage(float dt)
        {
            if (!_dealsDamage || _isTargeted)
                return;

            if (_time >= _totalDuration)
                return;

            if (World is not WalkableWorldControl { Walker: PlayerObject hero })
                return;

            if (hero.IsDead)
                return;

            if (MuGame.Network == null || !MuGame.Network.IsConnected)
                return;

            // Capture animation counter early (before any targets enter range), otherwise we may miss
            // the matching window and end up sending hits with AnimationCounter=0.
            EnsureAnimationCounterInitialized();

            Span<ushort> targetBuffer = stackalloc ushort[MaxHitTargets];
            int targetCount = CollectTargetsToHit(targetBuffer);
            if (targetCount <= 0)
                return;

            if (_animationCounter == 0)
            {
                _logger?.LogTrace(
                    "ScrollOfFlame: sending AreaSkillHit with AnimationCounter=0 (tile={X},{Y}, targets={Count}).",
                    _targetTileX, _targetTileY, targetCount);
            }

            var targets = new ushort[targetCount];
            for (int i = 0; i < targetCount; i++)
                targets[i] = targetBuffer[i];

            unchecked { _hitCounter++; }

            _logger?.LogTrace(
                "ScrollOfFlame: AreaSkillHit skill={SkillId} tile=({X},{Y}) targets={Count} hitCounter={HitCounter} animCounter={AnimCounter} t={Time:F2}s",
                FlameSkillId, _targetTileX, _targetTileY, targetCount, _hitCounter, _animationCounter, _time);

            _ = MuGame.Network
                .GetCharacterService()
                .SendAreaSkillHitAsync(FlameSkillId, _targetTileX, _targetTileY, _hitCounter, targets, _animationCounter);
        }

        private void EnsureAnimationCounterInitialized()
        {
            if (_animationCounter != 0)
                return;

            var characterState = MuGame.Network?.GetCharacterState();
            if (characterState == null)
                return;

            if (characterState.LastAreaSkillId != FlameSkillId)
                return;

            double nowMs = MuGame.Instance?.GameTime?.TotalGameTime.TotalMilliseconds ?? Environment.TickCount64;
            double elapsedMs = nowMs - characterState.LastAreaSkillSentAtMs;
            if (elapsedMs < 0 || elapsedMs > LastCastMatchWindowMs)
                return;

            if (characterState.LastAreaSkillTargetX != _targetTileX || characterState.LastAreaSkillTargetY != _targetTileY)
                return;

            _animationCounter = characterState.LastAreaSkillAnimationCounter;
        }

        private int CollectTargetsToHit(Span<ushort> targetBuffer)
        {
            if (World == null)
                return 0;

            float rangeSq = DamageRadius * DamageRadius;
            int count = 0;

            var monsters = World.Monsters;
            for (int i = 0; i < monsters.Count && count < targetBuffer.Length; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.IsDead)
                    continue;

                float dx = monster.Position.X - _center.X;
                float dy = monster.Position.Y - _center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                    continue;

                ushort targetId = monster.NetworkId;
                if (_nextHitTimeByTarget.TryGetValue(targetId, out float nextHitTime) && _time < nextHitTime)
                    continue;

                _nextHitTimeByTarget[targetId] = _time + DamageTickSeconds;
                targetBuffer[count++] = targetId;
            }

            return count;
        }

        private void RemoveSelf()
        {
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }

        public override void Dispose()
        {
            if (_lightsAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_flameLight);
                World.Terrain.RemoveDynamicLight(_topLight);
                _lightsAdded = false;
            }

            base.Dispose();
        }
    }
}
