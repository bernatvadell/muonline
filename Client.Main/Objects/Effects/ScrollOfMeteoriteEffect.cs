#nullable enable
using System;
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
    /// Ultimate Meteorite visual effect: fiery descent with magical accents,
    /// massive impact with debris, shockwave, sparks fountain, and dynamic lighting.
    /// </summary>
    public sealed class ScrollOfMeteoriteEffect : EffectObject
    {
        private const string CoreTexturePath = "Effect/flare01.jpg";
        private const string GlowTexturePath = "Effect/flare.jpg";
        private const string FireTexturePath = "Effect/Shiny05.jpg";
        private const string SparkTexturePath = "Effect/Spark03.jpg";
        private const string RingTexturePath = "Effect/Shiny02.jpg";

        private const int MaxParticles = 180;
        private const float DefaultDuration = 1.6f;
        private const float StartHeight = 900f;
        private const float ImpactHeightOffset = 35f;
        private const float LateralOffset = 120f;
        private const float TrailSpawnInterval = 0.008f;
        private const float TailLength = 380f;
        private const int TailSegments = 15;
        private const float ParticleGravity = 600f;
        private const float VisualScale = 15f;

        private readonly Vector3 _seedTarget;
        private Vector3 _targetPosition;
        private Vector3 _startPosition;
        private Vector3 _meteorPosition;
        private Vector3 _trailDirection;

        private readonly float _duration;
        private readonly float _fallDuration;
        private readonly float _impactDuration;
        private float _time;
        private float _impactAge;
        private float _trailSpawnTimer;
        private bool _positionsReady;
        private bool _impactTriggered;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _coreTexture = null!;
        private Texture2D _glowTexture = null!;
        private Texture2D _fireTexture = null!;
        private Texture2D _sparkTexture = null!;
        private Texture2D _ringTexture = null!;

        private readonly Particle[] _particles = new Particle[MaxParticles];

        private readonly DynamicLight _meteorLight;
        private readonly DynamicLight _impactLight;
        private readonly DynamicLight _ambientLight;
        private bool _lightsAdded;

        // Color palette
        private readonly Color _whiteHotCore = new(1f, 1f, 0.95f, 1f);
        private readonly Color _orangeCore = new(1f, 0.7f, 0.3f, 1f);
        private readonly Color _redFire = new(1f, 0.4f, 0.15f, 0.95f);
        private readonly Color _magicAccent = new(0.7f, 0.5f, 1f, 0.6f);
        private readonly Color _blueAccent = new(0.4f, 0.6f, 1f, 0.5f);
        private readonly Color _trailOrange = new(1f, 0.65f, 0.2f, 0.9f);
        private readonly Color _trailRed = new(1f, 0.35f, 0.1f, 0.85f);
        private readonly Color _impactWhite = new(1f, 0.95f, 0.85f, 1f);
        private readonly Color _impactOrange = new(1f, 0.55f, 0.2f, 1f);
        private readonly Color _debrisColor = new(0.85f, 0.7f, 0.5f, 0.9f);
        private readonly Color _smokeColor = new(0.35f, 0.32f, 0.3f, 0.7f);
        private readonly Color _shockwaveColor = new(1f, 0.8f, 0.5f, 0.8f);

        private enum ParticleKind : byte
        {
            TrailSpark = 0,
            ImpactSpark = 1,
            Smoke = 2,
            Debris = 3,
            MagicTrail = 4
        }

        private struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public float Scale;
            public ParticleKind Kind;
            public byte ColorVariant;
        }

        public ScrollOfMeteoriteEffect(Vector3 targetPosition, float durationSeconds = DefaultDuration)
        {
            _seedTarget = targetPosition;

            _duration = MathHelper.Clamp(durationSeconds, 0.8f, 2.5f);
            _fallDuration = MathHelper.Clamp(_duration * 0.55f, 0.3f, _duration - 0.25f);
            _impactDuration = MathF.Max(0.2f, _duration - _fallDuration);

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-500f, -500f, -150f),
                new Vector3(500f, 500f, 1050f));

            _meteorLight = new DynamicLight
            {
                Owner = this,
                Position = targetPosition,
                Color = new Vector3(1f, 0.65f, 0.35f),
                Radius = 320f,
                Intensity = 1.6f
            };

            _impactLight = new DynamicLight
            {
                Owner = this,
                Position = targetPosition,
                Color = new Vector3(1f, 0.5f, 0.25f),
                Radius = 520f,
                Intensity = 0f
            };

            _ambientLight = new DynamicLight
            {
                Owner = this,
                Position = targetPosition,
                Color = new Vector3(1f, 0.7f, 0.4f),
                Radius = 180f,
                Intensity = 0f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(CoreTexturePath);
            _ = await TextureLoader.Instance.Prepare(GlowTexturePath);
            _ = await TextureLoader.Instance.Prepare(FireTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);
            _ = await TextureLoader.Instance.Prepare(RingTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _coreTexture = TextureLoader.Instance.GetTexture2D(CoreTexturePath) ?? GraphicsManager.Instance.Pixel;
            _glowTexture = TextureLoader.Instance.GetTexture2D(GlowTexturePath) ?? GraphicsManager.Instance.Pixel;
            _fireTexture = TextureLoader.Instance.GetTexture2D(FireTexturePath) ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkTexturePath) ?? GraphicsManager.Instance.Pixel;
            _ringTexture = TextureLoader.Instance.GetTexture2D(RingTexturePath) ?? GraphicsManager.Instance.Pixel;

            if (World?.Terrain != null && !_lightsAdded)
            {
                World.Terrain.AddDynamicLight(_meteorLight);
                World.Terrain.AddDynamicLight(_impactLight);
                World.Terrain.AddDynamicLight(_ambientLight);
                _lightsAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
            {
                _ = Load();
            }

            if (Status != GameControlStatus.Ready)
                return;

            if (!_positionsReady)
            {
                InitializePositions();
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;

            if (_time >= _duration)
            {
                RemoveSelf();
                return;
            }

            float fallT = MathHelper.Clamp(_time / _fallDuration, 0f, 1f);
            float eased = fallT * fallT * (3f - 2f * fallT); // smoothstep for better motion
            _meteorPosition = Vector3.Lerp(_startPosition, _targetPosition, eased);

            if (!_impactTriggered && fallT >= 1f)
            {
                TriggerImpact();
            }

            if (_impactTriggered)
            {
                _impactAge += dt;
            }

            UpdateTrailParticles(dt);
            UpdateParticles(dt);
            UpdateDynamicLights();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _spriteBatch == null)
                return;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState, SamplerState.LinearClamp, DepthState))
                {
                    DrawLayers();
                }
            }
            else
            {
                DrawLayers();
            }
        }

        private void DrawLayers()
        {
            float lifeAlpha = MathHelper.Clamp(1f - (_time - _fallDuration) / _impactDuration, 0f, 1f);
            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.15f, 0f, 1f) : 1f;

            if (travelAlpha > 0f)
            {
                DrawMeteorBody(travelAlpha * lifeAlpha);
            }

            if (_impactTriggered)
            {
                DrawImpactEffects();
            }

            DrawParticles();
        }

        private void DrawMeteorBody(float alpha)
        {
            float pulse = 0.85f + 0.15f * MathF.Sin(_time * 22f);
            float fastPulse = 0.8f + 0.2f * MathF.Sin(_time * 35f);
            float slowPulse = 0.9f + 0.1f * MathF.Sin(_time * 8f);

            // Draw extended tail with color gradient
            DrawTail(alpha);

            // Outermost magical glow (purple/blue accents)
            DrawSprite(_glowTexture, _meteorPosition, _magicAccent * (alpha * 0.4f * slowPulse), _time * 1.2f, new Vector2(3.2f, 3.2f), VisualScale);
            DrawSprite(_glowTexture, _meteorPosition, _blueAccent * (alpha * 0.35f * slowPulse), -_time * 0.8f, new Vector2(2.8f, 2.8f), VisualScale);

            // Dark red outer glow
            DrawSprite(_glowTexture, _meteorPosition, _trailRed * (alpha * 0.7f), _time * 1.5f, new Vector2(2.6f, 2.6f), VisualScale);

            // Outer red fire layer - dominant
            DrawSprite(_fireTexture, _meteorPosition, _redFire * (alpha * 0.95f), _time * 3.5f, new Vector2(2.0f, 2.0f) * pulse, VisualScale);
            DrawSprite(_fireTexture, _meteorPosition, _redFire * (alpha * 0.85f), -_time * 2.8f, new Vector2(1.7f, 1.7f) * pulse, VisualScale);

            // Inner red/orange fire layer
            DrawSprite(_fireTexture, _meteorPosition, _trailRed * (alpha * 0.9f), -_time * 4.2f, new Vector2(1.4f, 1.4f) * pulse, VisualScale);
            DrawSprite(_fireTexture, _meteorPosition, _redFire * (alpha * 0.8f), _time * 5f, new Vector2(1.2f, 1.2f) * fastPulse, VisualScale);

            // Hot red-orange core
            DrawSprite(_coreTexture, _meteorPosition, _impactOrange * (alpha * 0.95f), _time * 5.5f, new Vector2(1.0f, 1.0f) * fastPulse, VisualScale);

            // Bright center with slight orange tint
            DrawSprite(_coreTexture, _meteorPosition, _orangeCore * (alpha * 0.9f), _time * 7f, new Vector2(0.6f, 0.6f) * fastPulse, VisualScale);
            DrawSprite(_coreTexture, _meteorPosition, _whiteHotCore * (alpha * 0.7f), -_time * 9f, new Vector2(0.35f, 0.35f), VisualScale);
        }

        private void DrawTail(float alpha)
        {
            Vector3 tailDir = _trailDirection;

            for (int i = 1; i <= TailSegments; i++)
            {
                float t = i / (float)TailSegments;
                float tCurved = t * t; // more density near meteor
                Vector3 pos = _meteorPosition + tailDir * (TailLength * tCurved);

                float scale = MathHelper.Lerp(2.0f, 0.4f, t);
                float fade = (1f - t * 0.7f) * alpha;

                // Color gradient: white → orange → red → dark
                Color segmentColor;
                if (t < 0.15f)
                    segmentColor = Color.Lerp(_whiteHotCore, _trailOrange, t / 0.15f);
                else if (t < 0.4f)
                    segmentColor = Color.Lerp(_trailOrange, _trailRed, (t - 0.15f) / 0.25f);
                else
                    segmentColor = Color.Lerp(_trailRed, _smokeColor, (t - 0.4f) / 0.6f);

                float rotation = _time * (3.5f - t * 2f) + t * 2f;
                DrawSprite(_glowTexture, pos, segmentColor * fade, rotation, new Vector2(scale, scale), VisualScale);

                // Add fire texture for first half of tail
                if (t < 0.5f)
                {
                    float fireScale = scale * 0.75f;
                    DrawSprite(_fireTexture, pos, _trailOrange * (fade * 0.65f), rotation * 1.3f, new Vector2(fireScale, fireScale), VisualScale);
                }

                // Add extra glow layer for early segments
                if (t < 0.25f)
                {
                    DrawSprite(_glowTexture, pos, _orangeCore * (fade * 0.45f), -rotation * 0.5f, new Vector2(scale * 1.2f, scale * 1.2f), VisualScale);
                }
            }

            // Magical trail on the side
            for (int i = 1; i <= 4; i++)
            {
                float t = i / 4f;
                Vector3 pos = _meteorPosition + tailDir * (TailLength * 0.35f * t);
                float scale = MathHelper.Lerp(1.0f, 0.35f, t);
                float fade = (1f - t) * alpha * 0.5f;
                float offset = 18f + t * 15f;
                DrawSprite(_glowTexture, pos + new Vector3(offset, offset, 0f), _magicAccent * fade, -_time * 2f + t, new Vector2(scale, scale), VisualScale);
                DrawSprite(_glowTexture, pos + new Vector3(-offset, offset, 0f), _blueAccent * (fade * 0.7f), _time * 1.5f + t, new Vector2(scale * 0.8f, scale * 0.8f), VisualScale);
            }
        }

        private void DrawImpactEffects()
        {
            float impactT = MathHelper.Clamp(_impactAge / _impactDuration, 0f, 1f);

            // Phase 1: Initial white flash
            if (impactT < 0.18f)
            {
                float flashT = impactT / 0.18f;
                float flashAlpha = 1f - flashT * flashT;
                float flashScale = MathHelper.Lerp(1.5f, 5f, flashT);

                DrawSprite(_coreTexture, _targetPosition, _impactWhite * flashAlpha, _impactAge * 8f, new Vector2(flashScale, flashScale), VisualScale);
                DrawSprite(_glowTexture, _targetPosition, _impactWhite * (flashAlpha * 0.75f), _impactAge * 4f, new Vector2(flashScale * 1.6f, flashScale * 1.6f), VisualScale);
                DrawSprite(_glowTexture, _targetPosition, _orangeCore * (flashAlpha * 0.55f), _impactAge * 2f, new Vector2(flashScale * 2f, flashScale * 2f), VisualScale);
            }

            // Phase 2: Orange/fire expansion
            if (impactT > 0.03f && impactT < 0.7f)
            {
                float fireT = (impactT - 0.03f) / 0.67f;
                float fireAlpha = MathF.Pow(1f - fireT, 1.4f);
                float fireScale = MathHelper.Lerp(2f, 6f, fireT);

                DrawSprite(_fireTexture, _targetPosition, _impactOrange * fireAlpha, _impactAge * 5f, new Vector2(fireScale, fireScale), VisualScale);
                DrawSprite(_fireTexture, _targetPosition, _redFire * (fireAlpha * 0.85f), -_impactAge * 4f, new Vector2(fireScale * 0.75f, fireScale * 0.75f), VisualScale);
                DrawSprite(_glowTexture, _targetPosition, _redFire * (fireAlpha * 0.6f), _impactAge * 3f, new Vector2(fireScale * 1.3f, fireScale * 1.3f), VisualScale);
            }

            // Shockwave ring
            if (impactT < 0.55f)
            {
                float ringT = impactT / 0.55f;
                float ringAlpha = MathF.Pow(1f - ringT, 1.8f);
                float ringScale = MathHelper.Lerp(1.2f, 8f, ringT);

                // Horizontal expanding ring (ellipse)
                DrawSprite(_ringTexture, _targetPosition, _shockwaveColor * ringAlpha, 0f, new Vector2(ringScale * 1.5f, ringScale * 0.55f), VisualScale);
                DrawSprite(_glowTexture, _targetPosition, _impactOrange * (ringAlpha * 0.5f), 0f, new Vector2(ringScale * 1.3f, ringScale * 0.45f), VisualScale);
            }

            // Ground fire ring
            if (impactT > 0.05f && impactT < 0.8f)
            {
                float groundT = (impactT - 0.05f) / 0.75f;
                float groundAlpha = MathF.Pow(1f - groundT, 1.7f) * 0.75f;
                float groundScale = MathHelper.Lerp(2f, 7f, groundT);

                Vector3 groundPos = _targetPosition - new Vector3(0f, 0f, 12f);
                DrawSprite(_fireTexture, groundPos, _trailOrange * groundAlpha, _impactAge * 2f, new Vector2(groundScale, groundScale * 0.45f), VisualScale);
                DrawSprite(_glowTexture, groundPos, _redFire * (groundAlpha * 0.5f), -_impactAge * 1.5f, new Vector2(groundScale * 1.1f, groundScale * 0.5f), VisualScale);
            }

            // Rising smoke column
            if (impactT > 0.1f)
            {
                float smokeT = (impactT - 0.1f) / 0.9f;

                // Multiple smoke puffs at different heights
                for (int i = 0; i < 3; i++)
                {
                    float puffDelay = i * 0.18f;
                    float puffT = MathHelper.Clamp((smokeT - puffDelay) / (1f - puffDelay), 0f, 1f);
                    if (puffT <= 0f) continue;

                    float smokeAlpha = MathF.Min(puffT * 2.5f, 1f) * (1f - puffT * 0.5f) * 0.6f;
                    float smokeHeight = puffT * 180f + i * 35f;
                    float smokeScale = MathHelper.Lerp(1.8f, 3.5f, puffT) * (1f - i * 0.15f);

                    Vector3 smokePos = _targetPosition + new Vector3(i * 6f - 6f, i * 5f - 5f, smokeHeight);
                    DrawSprite(_glowTexture, smokePos, _smokeColor * smokeAlpha, _impactAge * (0.3f + i * 0.1f), new Vector2(smokeScale, smokeScale), VisualScale);
                }
            }
        }

        private void DrawParticles()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                ref var p = ref _particles[i];
                if (p.Life <= 0f)
                    continue;

                float lifeRatio = p.Life / p.MaxLife;
                Color color;
                Texture2D texture;
                float alpha;
                float scale;
                float scaleMultiplier = VisualScale * 0.5f; // Base scale for particles

                switch (p.Kind)
                {
                    case ParticleKind.ImpactSpark:
                        color = p.ColorVariant switch
                        {
                            0 => _impactWhite,
                            1 => _impactOrange,
                            _ => _trailOrange
                        };
                        alpha = lifeRatio;
                        scale = p.Scale * (0.55f + 0.45f * lifeRatio);
                        texture = _sparkTexture;
                        scaleMultiplier = VisualScale * 0.6f;
                        break;

                    case ParticleKind.Debris:
                        color = _debrisColor;
                        alpha = lifeRatio * 0.85f;
                        scale = p.Scale * (0.65f + 0.35f * lifeRatio);
                        texture = _fireTexture;
                        scaleMultiplier = VisualScale * 0.55f;
                        break;

                    case ParticleKind.Smoke:
                        color = _smokeColor;
                        alpha = lifeRatio * 0.6f;
                        scale = p.Scale * (0.9f + 0.6f * (1f - lifeRatio)); // smoke expands
                        texture = _glowTexture;
                        scaleMultiplier = VisualScale * 0.7f;
                        break;

                    case ParticleKind.MagicTrail:
                        color = p.ColorVariant == 0 ? _magicAccent : _blueAccent;
                        alpha = lifeRatio * 0.75f;
                        scale = p.Scale * (0.55f + 0.45f * lifeRatio);
                        texture = _sparkTexture;
                        scaleMultiplier = VisualScale * 0.5f;
                        break;

                    default: // TrailSpark
                        color = p.ColorVariant == 0 ? _trailOrange : _trailRed;
                        alpha = lifeRatio;
                        scale = p.Scale * (0.55f + 0.45f * lifeRatio);
                        texture = _sparkTexture;
                        scaleMultiplier = VisualScale * 0.5f;
                        break;
                }

                DrawSprite(texture, p.Position, color * alpha, p.Rotation, new Vector2(scale, scale), scaleMultiplier);
            }
        }

        private void UpdateTrailParticles(float dt)
        {
            if (_impactTriggered)
                return;

            _trailSpawnTimer -= dt;
            while (_trailSpawnTimer <= 0f)
            {
                SpawnTrailSpark();
                if (MuGame.Random.NextDouble() < 0.3)
                    SpawnMagicTrailParticle();
                _trailSpawnTimer += TrailSpawnInterval;
            }
        }

        private void UpdateParticles(float dt)
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                ref var p = ref _particles[i];
                if (p.Life <= 0f)
                    continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Life = 0f;
                    continue;
                }

                p.Position += p.Velocity * dt;
                p.Rotation += p.RotationSpeed * dt;

                switch (p.Kind)
                {
                    case ParticleKind.Smoke:
                        // Smoke rises and slows down
                        p.Velocity += new Vector3(0f, 0f, 90f) * dt;
                        p.Velocity *= 0.98f;
                        break;

                    case ParticleKind.Debris:
                        // Debris has strong gravity and some drag
                        p.Velocity += new Vector3(0f, 0f, -ParticleGravity * 1.2f) * dt;
                        p.Velocity *= 0.995f;
                        break;

                    case ParticleKind.MagicTrail:
                        // Magic particles float slightly
                        p.Velocity += new Vector3(0f, 0f, -ParticleGravity * 0.3f) * dt;
                        p.Velocity *= 0.97f;
                        break;

                    default:
                        // Regular sparks
                        p.Velocity += new Vector3(0f, 0f, -ParticleGravity) * dt;
                        break;
                }
            }
        }

        private void UpdateDynamicLights()
        {
            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.15f, 0f, 1f) : 1f;
            float pulse = 0.85f + 0.15f * MathF.Sin(_time * 20f);
            float fastPulse = 0.9f + 0.1f * MathF.Sin(_time * 40f);

            // Meteor light follows the meteor
            _meteorLight.Position = _meteorPosition;
            _meteorLight.Intensity = 1.8f * travelAlpha * pulse;
            _meteorLight.Radius = MathHelper.Lerp(280f, 200f, 1f - travelAlpha);

            // Ambient magical glow
            _ambientLight.Position = _meteorPosition + _trailDirection * 60f;
            _ambientLight.Color = new Vector3(0.7f, 0.5f, 1f); // purple tint
            _ambientLight.Intensity = 0.4f * travelAlpha * fastPulse;
            _ambientLight.Radius = 140f;

            // Impact light
            float impactT = _impactTriggered ? MathHelper.Clamp(_impactAge / _impactDuration, 0f, 1f) : 0f;

            if (_impactTriggered)
            {
                // Bright flash at start, then fade
                float flashIntensity;
                if (impactT < 0.1f)
                {
                    flashIntensity = MathHelper.Lerp(0f, 3.5f, impactT / 0.1f);
                }
                else
                {
                    flashIntensity = 3.5f * MathF.Pow(1f - (impactT - 0.1f) / 0.9f, 1.5f);
                }

                _impactLight.Position = _targetPosition;
                _impactLight.Intensity = flashIntensity;
                _impactLight.Radius = MathHelper.Lerp(550f, 350f, impactT);
            }
        }

        private void TriggerImpact()
        {
            _impactTriggered = true;
            _impactAge = 0f;

            // Spawn impact particles
            SpawnImpactSparks(30);
            SpawnDebris(16);
            SpawnSmoke(12);
        }

        private void SpawnImpactSparks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FindDeadParticle();
                if (slot < 0)
                    return;

                Vector3 dir = RandomUnitVector3(0.2f, 0.95f);
                float speed = RandomRange(180f, 400f);
                float life = RandomRange(0.4f, 0.8f);

                _particles[slot] = new Particle
                {
                    Position = _targetPosition + RandomJitter(20f, 20f, 14f),
                    Velocity = dir * speed,
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    RotationSpeed = RandomRange(3f, 8f) * (MuGame.Random.NextDouble() < 0.5 ? 1f : -1f),
                    Scale = RandomRange(1.2f, 2.4f),
                    Kind = ParticleKind.ImpactSpark,
                    ColorVariant = (byte)MuGame.Random.Next(0, 3)
                };
            }
        }

        private void SpawnDebris(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FindDeadParticle();
                if (slot < 0)
                    return;

                // Debris shoots upward and outward
                Vector3 dir = RandomUnitVector3(0.4f, 0.9f);
                float speed = RandomRange(220f, 380f);
                float life = RandomRange(0.6f, 1.1f);

                _particles[slot] = new Particle
                {
                    Position = _targetPosition + RandomJitter(24f, 24f, 10f),
                    Velocity = dir * speed,
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    RotationSpeed = RandomRange(4f, 10f) * (MuGame.Random.NextDouble() < 0.5 ? 1f : -1f),
                    Scale = RandomRange(1.5f, 2.8f),
                    Kind = ParticleKind.Debris,
                    ColorVariant = 0
                };
            }
        }

        private void SpawnSmoke(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FindDeadParticle();
                if (slot < 0)
                    return;

                Vector3 dir = RandomUnitVector3(0.1f, 0.5f);
                float speed = RandomRange(50f, 100f);
                float life = RandomRange(0.8f, 1.4f);

                _particles[slot] = new Particle
                {
                    Position = _targetPosition + RandomJitter(28f, 28f, 8f),
                    Velocity = dir * speed + new Vector3(0f, 0f, 65f),
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    RotationSpeed = RandomRange(0.3f, 1.0f),
                    Scale = RandomRange(2.0f, 3.8f),
                    Kind = ParticleKind.Smoke,
                    ColorVariant = 0
                };
            }
        }

        private void SpawnTrailSpark()
        {
            int slot = FindDeadParticle();
            if (slot < 0)
                return;

            Vector3 dir = _trailDirection;
            Vector3 lateral = RandomUnitVector3(-0.3f, 0.3f) * RandomRange(22f, 70f);
            Vector3 velocity = dir * RandomRange(65f, 140f) + lateral;
            float life = RandomRange(0.22f, 0.48f);

            _particles[slot] = new Particle
            {
                Position = _meteorPosition + RandomJitter(15f, 15f, 12f),
                Velocity = velocity,
                Life = life,
                MaxLife = life,
                Rotation = RandomRange(0f, MathHelper.TwoPi),
                RotationSpeed = RandomRange(3f, 7f),
                Scale = RandomRange(0.9f, 1.8f),
                Kind = ParticleKind.TrailSpark,
                ColorVariant = (byte)(MuGame.Random.NextDouble() < 0.6 ? 0 : 1)
            };
        }

        private void SpawnMagicTrailParticle()
        {
            int slot = FindDeadParticle();
            if (slot < 0)
                return;

            Vector3 lateral = RandomUnitVector3(-0.45f, 0.45f) * RandomRange(28f, 60f);
            Vector3 velocity = _trailDirection * RandomRange(40f, 85f) + lateral;
            float life = RandomRange(0.3f, 0.55f);

            _particles[slot] = new Particle
            {
                Position = _meteorPosition + RandomJitter(18f, 18f, 14f),
                Velocity = velocity,
                Life = life,
                MaxLife = life,
                Rotation = RandomRange(0f, MathHelper.TwoPi),
                RotationSpeed = RandomRange(1f, 3f),
                Scale = RandomRange(0.7f, 1.4f),
                Kind = ParticleKind.MagicTrail,
                ColorVariant = (byte)(MuGame.Random.NextDouble() < 0.5 ? 0 : 1)
            };
        }

        private int FindDeadParticle()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (_particles[i].Life <= 0f)
                    return i;
            }

            return -1;
        }

        private void InitializePositions()
        {
            _targetPosition = _seedTarget;

            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_seedTarget.X, _seedTarget.Y);
                _targetPosition = new Vector3(_seedTarget.X, _seedTarget.Y, groundZ + ImpactHeightOffset);
            }

            float offsetX = RandomRange(-LateralOffset, LateralOffset);
            float offsetY = RandomRange(-LateralOffset, LateralOffset);
            _startPosition = _targetPosition + new Vector3(offsetX, offsetY, StartHeight);

            _meteorPosition = _startPosition;

            _trailDirection = _startPosition - _targetPosition;
            if (_trailDirection.LengthSquared() > 0.001f)
            {
                _trailDirection.Normalize();
            }
            else
            {
                _trailDirection = Vector3.UnitZ;
            }

            UpdateBounds(_startPosition, _targetPosition);
            _positionsReady = true;
        }

        private void UpdateBounds(Vector3 start, Vector3 target)
        {
            Vector3 min = Vector3.Min(start, target);
            Vector3 max = Vector3.Max(start, target);
            Vector3 pad = new Vector3(300f, 300f, 200f);
            min -= pad;
            max += pad;

            Vector3 center = (min + max) * 0.5f;
            Position = center;
            BoundingBoxLocal = new BoundingBox(min - center, max - center);
        }

        private void RemoveSelf()
        {
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }

        private static float RandomRange(float min, float max)
        {
            return (float)(MuGame.Random.NextDouble() * (max - min) + min);
        }

        private static Vector3 RandomJitter(float rangeX, float rangeY, float rangeZ)
        {
            return new Vector3(
                RandomRange(-rangeX, rangeX),
                RandomRange(-rangeY, rangeY),
                RandomRange(-rangeZ, rangeZ));
        }

        private static Vector3 RandomUnitVector3(float zMin, float zMax)
        {
            float z = RandomRange(zMin, zMax);
            float theta = RandomRange(0f, MathHelper.TwoPi);
            float r = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
            return new Vector3(r * MathF.Cos(theta), r * MathF.Sin(theta), z);
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, Vector2 scale, float scaleMultiplier = 1f)
        {
            if (texture == null)
                return;

            var viewport = GraphicsDevice.Viewport;
            Vector3 projected = viewport.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            Vector2 finalScale = scale * baseScale * scaleMultiplier;
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

        private static float ComputeScreenScale(Vector3 worldPos, float baseScale)
        {
            float distance = Vector3.Distance(Camera.Instance.Position, worldPos);
            float scale = baseScale / (MathF.Max(distance, 0.1f) / Constants.TERRAIN_SIZE);
            return scale * Constants.RENDER_SCALE;
        }

        public override void Dispose()
        {
            if (_lightsAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_meteorLight);
                World.Terrain.RemoveDynamicLight(_impactLight);
                World.Terrain.RemoveDynamicLight(_ambientLight);
                _lightsAdded = false;
            }

            base.Dispose();
        }
    }
}
