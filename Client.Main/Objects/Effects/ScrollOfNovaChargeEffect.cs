#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Nova charging effect (Skill ID 58).
    /// Stage intensity is driven by SkillStageUpdate (0xBA) packets.
    /// </summary>
    public sealed class ScrollOfNovaChargeEffect : EffectObject
    {
        private const string SpiritTexturePath = "Effect/JointSpirit01.jpg";
        private const string FlareTexturePath = "Effect/flare.jpg";
        private const float MaxNovaStage = 12f;
        private const int MaxParticles = 240;
        private const float CoreZOffset = 110f;
        private const float MinSpawnRadius = 70f;
        private const float MaxSpawnRadius = 300f;

        private static readonly Dictionary<WalkableWorldControl, Dictionary<ushort, ScrollOfNovaChargeEffect>> ActiveByWorld =
            new(ReferenceEqualityComparer.Instance);
        private static readonly Dictionary<WalkableWorldControl, Dictionary<ushort, byte>> LastStageByWorld =
            new(ReferenceEqualityComparer.Instance);

        private readonly WalkerObject _caster;
        private readonly DynamicLight _chargeLight;
        private readonly ChargeRayParticle[] _particles = new ChargeRayParticle[MaxParticles];
        private readonly ushort _casterId;

        private WalkableWorldControl? _registeredWorld;
        private int _particleCount;
        private float _spawnTimer;
        private float _opacity;
        private float _targetOpacity;
        private bool _finishing;
        private byte _stage;
        private double _lastSignalMs;
        private bool _lightAdded;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _spiritTexture = null!;
        private Texture2D _flareTexture = null!;

        private struct ChargeRayParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public float Width;
            public float Length;
            public float AlphaScale;
            public float Rotation;
        }

        private ScrollOfNovaChargeEffect(WalkerObject caster)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _casterId = caster.NetworkId;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-260f, -260f, -60f),
                new Vector3(260f, 260f, 320f));

            _chargeLight = new DynamicLight
            {
                Owner = this,
                Position = caster.WorldPosition.Translation,
                Color = new Vector3(0.35f, 0.45f, 0.95f),
                Radius = 180f,
                Intensity = 0.1f
            };

            RefreshSignal();
            _targetOpacity = 0.45f;
            _opacity = 0.25f;
        }

        public static ScrollOfNovaChargeEffect GetOrCreate(WalkableWorldControl world, WalkerObject caster)
        {
            if (world == null || caster == null)
                throw new ArgumentNullException(world == null ? nameof(world) : nameof(caster));

            ushort casterId = caster.NetworkId;
            if (TryGetActive(world, casterId, out var active))
            {
                active.RefreshSignal();
                return active;
            }

            var effect = new ScrollOfNovaChargeEffect(caster);
            Register(world, casterId, effect);

            world.Objects.Add(effect);
            _ = effect.Load();

            return effect;
        }

        public static void StopForCaster(WalkableWorldControl world, ushort casterId)
        {
            if (world == null)
                return;

            if (TryGetActive(world, casterId, out var effect))
            {
                effect.BeginFinish();
            }
        }

        public static void UpdateStage(WalkableWorldControl world, ushort casterId, byte stage)
        {
            if (world == null)
                return;

            RememberStage(world, casterId, stage);

            if (TryGetActive(world, casterId, out var effect))
            {
                effect.SetStage(stage);
                return;
            }

            if (!world.TryGetWalkerById(casterId, out var caster) || caster == null)
                return;

            var created = GetOrCreate(world, caster);
            created.SetStage(stage);
        }

        public static byte ConsumeStageAndStop(WalkableWorldControl world, ushort casterId)
        {
            byte stage = 0;

            if (world == null)
                return stage;

            if (TryGetActive(world, casterId, out var effect))
            {
                stage = effect._stage;
                effect.BeginFinish();
            }
            else if (TryGetRememberedStage(world, casterId, out var remembered))
            {
                stage = remembered;
            }

            if (LastStageByWorld.TryGetValue(world, out var byCaster))
            {
                byCaster.Remove(casterId);
                if (byCaster.Count == 0)
                    LastStageByWorld.Remove(world);
            }

            return stage;
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(SpiritTexturePath);
            _ = await TextureLoader.Instance.Prepare(FlareTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _spiritTexture = TextureLoader.Instance.GetTexture2D(SpiritTexturePath) ?? GraphicsManager.Instance.Pixel;
            _flareTexture = TextureLoader.Instance.GetTexture2D(FlareTexturePath) ?? _spiritTexture;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
                _ = Load();

            if (Status != GameControlStatus.Ready)
                return;

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            Vector3 anchor = _caster.WorldPosition.Translation + new Vector3(0f, 0f, CoreZOffset);
            Position = anchor;

            if (!_finishing)
            {
                double nowMs = GetNowMs();
                if (nowMs - _lastSignalMs > 3500.0)
                    BeginFinish();
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float lerpSpeed = _finishing ? 6f : 8f;
            _opacity = MathHelper.Lerp(_opacity, _targetOpacity, MathHelper.Clamp(dt * lerpSpeed, 0f, 1f));

            SpawnParticles(dt, anchor);
            UpdateParticles(dt, anchor);
            UpdateLight(anchor);

            if (_finishing && _opacity <= 0.02f && _particleCount == 0)
            {
                RemoveSelf();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _opacity <= 0.01f || _spriteBatch == null)
                return;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthState))
                    DrawParticles();
            }
            else
                DrawParticles();
        }

        public void SetStage(byte stage)
        {
            _stage = (byte)Math.Clamp((int)stage, 0, (int)MaxNovaStage);
            RefreshSignal();
            RememberStage(_registeredWorld, _casterId, _stage);

            float stageFactor = _stage / MaxNovaStage;
            _targetOpacity = MathHelper.Lerp(0.45f, 1f, stageFactor);
        }

        public void BeginFinish()
        {
            _finishing = true;
            _targetOpacity = 0f;
        }

        private void RefreshSignal()
        {
            _finishing = false;
            _lastSignalMs = GetNowMs();
        }

        private void SpawnParticles(float dt, Vector3 anchor)
        {
            if (_opacity <= 0.01f)
                return;

            float stageFactor = _stage / MaxNovaStage;
            float visualFactor = MathF.Max(0.35f, stageFactor);
            float spawnRate = MathHelper.Lerp(90f, 240f, visualFactor);
            _spawnTimer += dt * spawnRate;

            int spawnCount = (int)_spawnTimer;
            if (spawnCount <= 0)
                return;

            _spawnTimer -= spawnCount;

            for (int i = 0; i < spawnCount; i++)
            {
                if (_particleCount >= MaxParticles)
                    break;

                float angle = RandomRange(0f, MathHelper.TwoPi);
                float radius = MathHelper.Lerp(MinSpawnRadius, MaxSpawnRadius, visualFactor) + RandomRange(-22f, 26f);
                float zJitter = RandomRange(-40f, 125f);
                Vector3 radial = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
                Vector3 spawnPos = anchor + (radial * radius) + new Vector3(0f, 0f, zJitter);

                Vector3 toAnchor = anchor - spawnPos;
                float toAnchorLen = MathF.Max(1f, toAnchor.Length());
                Vector3 inwardDir = toAnchor / toAnchorLen;
                Vector3 tangent = new Vector3(-radial.Y, radial.X, 0f) * RandomRange(20f, 95f);
                Vector3 velocity = inwardDir * MathHelper.Lerp(240f, 560f, visualFactor) + tangent;

                _particles[_particleCount++] = new ChargeRayParticle
                {
                    Position = spawnPos,
                    Velocity = velocity,
                    Age = 0f,
                    Lifetime = RandomRange(0.28f, 0.70f),
                    Width = RandomRange(44f, 84f),
                    Length = RandomRange(240f, 520f),
                    AlphaScale = RandomRange(0.82f, 1f),
                    Rotation = 0f
                };
            }
        }

        private void UpdateParticles(float dt, Vector3 anchor)
        {
            int i = 0;
            while (i < _particleCount)
            {
                ref var particle = ref _particles[i];
                particle.Age += dt;
                if (particle.Age >= particle.Lifetime)
                {
                    _particles[i] = _particles[--_particleCount];
                    continue;
                }

                Vector3 toAnchor = anchor - particle.Position;
                float dist = toAnchor.Length();
                if (dist > 0.001f)
                {
                    // Keep rays converging to the caster, like the original Nova charge.
                    Vector3 accel = (toAnchor / dist) * 780f;
                    particle.Velocity += accel * dt;
                }

                particle.Position += particle.Velocity * dt;
                particle.Velocity *= 1f - MathHelper.Clamp(dt * 1.4f, 0f, 0.22f);
                particle.Velocity.Z *= 1f - MathHelper.Clamp(dt * 0.35f, 0f, 0.08f);
                particle.Rotation += dt * 4f;

                if (dist < 14f)
                {
                    particle.Age = particle.Lifetime;
                }

                i++;
            }
        }

        private void DrawParticles()
        {
            for (int i = 0; i < _particleCount; i++)
            {
                ref var particle = ref _particles[i];
                float life = 1f - (particle.Age / particle.Lifetime);
                float alpha = life * _opacity * particle.AlphaScale;
                if (alpha <= 0.01f)
                    continue;

                Color glowColor = new Color(0.18f * alpha, 0.34f * alpha, 1f * alpha, alpha);
                Color coreColor = new Color(0.10f * alpha, 0.24f * alpha, 0.85f * alpha, alpha);
                float width = particle.Width * (0.75f + (1f - life) * 0.55f);
                float length = particle.Length * (0.9f + (1f - life) * 0.25f);

                DrawRaySprite(_flareTexture, particle.Position, particle.Velocity, glowColor, width * 1.35f, length * 1.15f);
                DrawRaySprite(_spiritTexture, particle.Position, particle.Velocity, coreColor, width, length);
            }
        }

        private void DrawRaySprite(Texture2D texture, Vector3 worldPos, Vector3 velocity, Color color, float width, float length)
        {
            var camera = Camera.Instance;
            if (camera == null)
                return;

            var viewport = GraphicsDevice.Viewport;
            Vector3 projectedHead = viewport.Project(worldPos, camera.Projection, camera.View, Matrix.Identity);
            if (projectedHead.Z < 0f || projectedHead.Z > 1f)
                return;

            Vector3 dir = velocity;
            if (dir.LengthSquared() < 0.0001f)
                return;

            dir.Normalize();
            Vector3 projectedTail = viewport.Project(worldPos + (dir * length), camera.Projection, camera.View, Matrix.Identity);
            Vector2 screenHead = new Vector2(projectedHead.X, projectedHead.Y);
            Vector2 screenTail = new Vector2(projectedTail.X, projectedTail.Y);
            Vector2 screenDir = screenTail - screenHead;
            float screenLength = screenDir.Length();
            if (screenLength <= 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            float angle = MathF.Atan2(screenDir.Y, screenDir.X);
            float scaleX = screenLength / MathF.Max(1f, texture.Width);
            float scaleY = (width * baseScale) / MathF.Max(1f, texture.Height);
            float depth = MathHelper.Clamp(projectedHead.Z, 0f, 1f);

            _spriteBatch.Draw(
                texture,
                screenHead,
                null,
                color,
                angle,
                new Vector2(0f, texture.Height * 0.5f),
                new Vector2(scaleX, MathF.Max(0.01f, scaleY)),
                SpriteEffects.None,
                depth);
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, float scale)
        {
            var camera = Camera.Instance;
            if (camera == null)
                return;

            var viewport = GraphicsDevice.Viewport;
            Vector3 projected = viewport.Project(worldPos, camera.Projection, camera.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            float finalScale = Math.Max(0.01f, scale * baseScale);
            float depth = MathHelper.Clamp(projected.Z, 0f, 1f);

            _spriteBatch.Draw(
                texture,
                new Vector2(projected.X, projected.Y),
                null,
                color,
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                finalScale,
                SpriteEffects.None,
                depth);
        }

        private void UpdateLight(Vector3 anchor)
        {
            if (World?.Terrain != null)
            {
                if (!_lightAdded)
                {
                    World.Terrain.AddDynamicLight(_chargeLight);
                    _lightAdded = true;
                }
            }
            else if (_lightAdded)
            {
                World?.Terrain?.RemoveDynamicLight(_chargeLight);
                _lightAdded = false;
            }

            float stageFactor = _stage / MaxNovaStage;
            _chargeLight.Position = anchor;
            _chargeLight.Radius = MathHelper.Lerp(140f, 320f, stageFactor) * MathHelper.Lerp(0.55f, 1f, _opacity);
            _chargeLight.Intensity = MathHelper.Lerp(0.15f, 1.25f, stageFactor) * _opacity;
        }

        private static float ComputeScreenScale(Vector3 worldPos, float baseScale)
        {
            float distance = Vector3.Distance(Camera.Instance.Position, worldPos);
            float scale = baseScale / (MathF.Max(distance, 0.1f) / Constants.TERRAIN_SIZE);
            return scale * Constants.RENDER_SCALE;
        }

        private static float RandomRange(float min, float max)
        {
            return (float)(MuGame.Random.NextDouble() * (max - min) + min);
        }

        private static double GetNowMs()
        {
            var gameTime = MuGame.Instance?.GameTime;
            if (gameTime != null)
                return gameTime.TotalGameTime.TotalMilliseconds;

            return Environment.TickCount64;
        }

        private static float GetNowSeconds()
        {
            var gameTime = MuGame.Instance?.GameTime;
            if (gameTime != null)
                return (float)gameTime.TotalGameTime.TotalSeconds;

            return Environment.TickCount64 * 0.001f;
        }

        private static bool TryGetActive(WalkableWorldControl world, ushort casterId, out ScrollOfNovaChargeEffect effect)
        {
            effect = null!;
            if (!ActiveByWorld.TryGetValue(world, out var byCaster))
                return false;

            if (!byCaster.TryGetValue(casterId, out var found) || found == null)
                return false;

            effect = found;
            if (effect == null || effect.Status == GameControlStatus.Disposed)
            {
                byCaster.Remove(casterId);
                if (byCaster.Count == 0)
                    ActiveByWorld.Remove(world);

                effect = null!;
                return false;
            }

            return true;
        }

        private static void Register(WalkableWorldControl world, ushort casterId, ScrollOfNovaChargeEffect effect)
        {
            if (!ActiveByWorld.TryGetValue(world, out var byCaster))
            {
                byCaster = new Dictionary<ushort, ScrollOfNovaChargeEffect>();
                ActiveByWorld[world] = byCaster;
            }

            byCaster[casterId] = effect;
            effect._registeredWorld = world;

            if (TryGetRememberedStage(world, casterId, out var remembered))
                effect.SetStage(remembered);
        }

        private static void RememberStage(WalkableWorldControl? world, ushort casterId, byte stage)
        {
            if (world == null)
                return;

            if (!LastStageByWorld.TryGetValue(world, out var byCaster))
            {
                byCaster = new Dictionary<ushort, byte>();
                LastStageByWorld[world] = byCaster;
            }

            byCaster[casterId] = stage;
        }

        private static bool TryGetRememberedStage(WalkableWorldControl world, ushort casterId, out byte stage)
        {
            stage = 0;
            return LastStageByWorld.TryGetValue(world, out var byCaster) && byCaster.TryGetValue(casterId, out stage);
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
            if (_lightAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_chargeLight);
                _lightAdded = false;
            }

            if (_registeredWorld != null)
            {
                if (ActiveByWorld.TryGetValue(_registeredWorld, out var byCaster))
                {
                    byCaster.Remove(_casterId);
                    if (byCaster.Count == 0)
                        ActiveByWorld.Remove(_registeredWorld);
                }

                RememberStage(_registeredWorld, _casterId, _stage);
                _registeredWorld = null;
            }

            base.Dispose();
        }
    }
}
