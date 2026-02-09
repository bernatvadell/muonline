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
    /// Energy Ball visual effect (original MU uses BITMAP_ENERGY / Thunder01).
    /// </summary>
    public sealed class ScrollOfEnergyBallEffect : EffectObject
    {
        private const string EnergyTexturePath = "Effect/Thunder01.OZJ";
        private const string GlowTexturePath = "Effect/flare.OZJ";
        private const string SparkTexturePath = "Effect/Spark03.OZJ";

        private const int MaxSparks = 40;
        private const float DefaultSpeed = 2400f;
        private const float ImpactDuration = 0.18f;
        private const float TailLength = 180f;
        private const int TailSegments = 5;
        private const float SparkSpawnInterval = 0.018f;
        private const float SparkGravity = 320f;
        private const float VisualScale = 12f;
        private const float SparkVisualScale = 8f;

        private readonly Vector3 _startPosition;
        private readonly Vector3 _targetPosition;
        private Vector3 _currentPosition;
        private Vector3 _direction;

        private readonly float _duration;
        private readonly float _totalDuration;
        private float _time;
        private float _impactAge;
        private bool _impactTriggered;
        private float _spin;
        private float _sparkTimer;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _energyTexture = null!;
        private Texture2D _glowTexture = null!;
        private Texture2D _sparkTexture = null!;

        private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];

        private readonly DynamicLight _ballLight;
        private readonly DynamicLight _impactLight;
        private bool _lightsAdded;

        private readonly Color _coreColor = new(0.25f, 0.65f, 1.0f, 1f);
        private readonly Color _glowColor = new(0.35f, 0.8f, 1.0f, 0.85f);
        private readonly Color _tailColor = new(0.2f, 0.55f, 1.0f, 0.9f);
        private readonly Color _sparkColor = new(0.6f, 0.85f, 1.0f, 1f);
        private readonly Color _smokeColor = new(0.25f, 0.35f, 0.5f, 0.6f);

        private struct SparkParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float Scale;
            public byte Kind;
        }

        public ScrollOfEnergyBallEffect(Vector3 startPosition, Vector3 targetPosition, float speed = DefaultSpeed)
        {
            _startPosition = startPosition;
            _targetPosition = targetPosition;
            _currentPosition = startPosition;

            Vector3 delta = targetPosition - startPosition;
            float distance = delta.Length();
            if (distance > 0.001f)
                _direction = delta / distance;
            else
                _direction = Vector3.UnitY;

            float effectiveSpeed = MathHelper.Clamp(speed, 800f, 4600f);
            _duration = MathHelper.Clamp(distance / MathF.Max(1f, effectiveSpeed), 0.16f, 1.1f);
            _totalDuration = _duration + ImpactDuration;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-140f, -140f, -120f),
                new Vector3(140f, 140f, 140f));

            _ballLight = new DynamicLight
            {
                Owner = this,
                Position = startPosition,
                Color = new Vector3(0.25f, 0.6f, 1.0f),
                Radius = 180f,
                Intensity = 1.05f
            };

            _impactLight = new DynamicLight
            {
                Owner = this,
                Position = targetPosition,
                Color = new Vector3(0.25f, 0.55f, 1.0f),
                Radius = 280f,
                Intensity = 0f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(EnergyTexturePath);
            _ = await TextureLoader.Instance.Prepare(GlowTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _energyTexture = TextureLoader.Instance.GetTexture2D(EnergyTexturePath) ?? GraphicsManager.Instance.Pixel;
            _glowTexture = TextureLoader.Instance.GetTexture2D(GlowTexturePath) ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkTexturePath) ?? GraphicsManager.Instance.Pixel;

            if (World?.Terrain != null && !_lightsAdded)
            {
                World.Terrain.AddDynamicLight(_ballLight);
                World.Terrain.AddDynamicLight(_impactLight);
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

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;
            _spin += dt * 7.5f;

            if (_time <= _duration)
            {
                float t = MathHelper.Clamp(_time / _duration, 0f, 1f);
                float eased = 1f - MathF.Pow(1f - t, 2f);
                _currentPosition = Vector3.Lerp(_startPosition, _targetPosition, eased);
                SpawnTrailSparks(dt);
            }
            else
            {
                if (!_impactTriggered)
                    TriggerImpact();

                _impactAge += dt;
            }

            UpdateSparks(dt);
            UpdateDynamicLights();

            Position = _currentPosition;

            if (_time >= _totalDuration)
                RemoveSelf();
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
            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.08f, 0f, 1f) : 1f;
            float pulse = 0.85f + 0.15f * MathF.Sin(_time * 18f);

            if (travelAlpha > 0f)
            {
                DrawTail(travelAlpha);
                DrawBall(travelAlpha * pulse);
            }

            if (_impactTriggered)
                DrawImpactFlash();

            DrawSparks();
        }

        private void DrawBall(float alpha)
        {
            float orbit = _spin * 1.6f;
            float orbitRadius = 12f + 4f * MathF.Sin(_spin * 1.4f);
            Vector3 orbitOffset = new Vector3(MathF.Cos(orbit) * orbitRadius, MathF.Sin(orbit) * orbitRadius, MathF.Sin(orbit * 1.2f) * 5f);

            DrawSprite(_energyTexture, _currentPosition, _coreColor * alpha, _spin * 2.2f, new Vector2(0.95f, 0.95f), VisualScale);
            DrawSprite(_glowTexture, _currentPosition, _glowColor * (alpha * 0.7f), -_spin * 1.2f, new Vector2(1.6f, 1.6f), VisualScale);
            DrawSprite(_energyTexture, _currentPosition + orbitOffset, _coreColor * (alpha * 0.5f), orbit, new Vector2(0.55f, 0.55f), VisualScale);
            DrawSprite(_energyTexture, _currentPosition - orbitOffset, _coreColor * (alpha * 0.5f), -orbit, new Vector2(0.55f, 0.55f), VisualScale);
        }

        private void DrawTail(float alpha)
        {
            Vector3 tailDir = -_direction;
            for (int i = 1; i <= TailSegments; i++)
            {
                float t = i / (float)TailSegments;
                Vector3 pos = _currentPosition + tailDir * (TailLength * t);
                float scale = MathHelper.Lerp(1.0f, 0.25f, t);
                float fade = (1f - t) * alpha;
                DrawSprite(_energyTexture, pos, _tailColor * fade, _spin * 1.1f + t, new Vector2(scale, scale), VisualScale);
            }
        }

        private void DrawImpactFlash()
        {
            float impactT = MathHelper.Clamp(_impactAge / ImpactDuration, 0f, 1f);
            float flashAlpha = MathF.Pow(1f - impactT, 1.6f);
            float flashScale = MathHelper.Lerp(1.0f, 3.0f, impactT);

            DrawSprite(_energyTexture, _targetPosition, _coreColor * flashAlpha, _impactAge * 6f, new Vector2(flashScale, flashScale), VisualScale);
            DrawSprite(_glowTexture, _targetPosition, _glowColor * (flashAlpha * 0.65f), _impactAge * 2.2f, new Vector2(flashScale * 1.4f, flashScale * 1.4f), VisualScale);
        }

        private void DrawSparks()
        {
            for (int i = 0; i < MaxSparks; i++)
            {
                if (_sparks[i].Life <= 0f)
                    continue;

                float lifeRatio = _sparks[i].Life / _sparks[i].MaxLife;
                Color color = _sparks[i].Kind == 2 ? _smokeColor : _sparkColor;
                float alpha = _sparks[i].Kind == 2 ? lifeRatio * 0.6f : lifeRatio;
                float scale = _sparks[i].Scale * (0.7f + 0.3f * lifeRatio) * SparkVisualScale;

                DrawSprite(_sparkTexture, _sparks[i].Position, color * alpha, _sparks[i].Rotation, new Vector2(scale, scale));
            }
        }

        private void SpawnTrailSparks(float dt)
        {
            _sparkTimer -= dt;
            while (_sparkTimer <= 0f)
            {
                int slot = FindDeadSpark();
                if (slot >= 0)
                {
                    Vector3 lateral = RandomUnitVector3(-0.2f, 0.25f) * RandomRange(8f, 50f);
                    Vector3 velocity = (-_direction * RandomRange(70f, 140f)) + lateral;
                    float life = RandomRange(0.18f, 0.35f);

                    _sparks[slot] = new SparkParticle
                    {
                        Position = _currentPosition + RandomJitter(8f, 8f, 5f),
                        Velocity = velocity,
                        Life = life,
                        MaxLife = life,
                        Rotation = RandomRange(0f, MathHelper.TwoPi),
                        Scale = RandomRange(0.5f, 1.0f),
                        Kind = 0
                    };
                }

                _sparkTimer += SparkSpawnInterval;
            }
        }

        private void UpdateSparks(float dt)
        {
            for (int i = 0; i < MaxSparks; i++)
            {
                if (_sparks[i].Life <= 0f)
                    continue;

                var spark = _sparks[i];
                spark.Life -= dt;
                if (spark.Life <= 0f)
                {
                    spark.Life = 0f;
                    _sparks[i] = spark;
                    continue;
                }

                spark.Position += spark.Velocity * dt;
                spark.Velocity += new Vector3(0f, 0f, -SparkGravity) * dt;
                spark.Rotation += dt * 4.2f;
                _sparks[i] = spark;
            }
        }

        private void UpdateDynamicLights()
        {
            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.08f, 0f, 1f) : 1f;
            float pulse = 0.75f + 0.25f * MathF.Sin(_time * 16f);

            _ballLight.Position = _currentPosition;
            _ballLight.Intensity = 1.0f * travelAlpha * pulse;
            _ballLight.Radius = 170f + 18f * MathF.Sin(_time * 8f);

            float impactT = _impactTriggered ? MathHelper.Clamp(_impactAge / ImpactDuration, 0f, 1f) : 0f;
            float impactAlpha = _impactTriggered ? MathF.Pow(1f - impactT, 1.3f) : 0f;

            _impactLight.Position = _targetPosition;
            _impactLight.Intensity = 1.6f * impactAlpha;
            _impactLight.Radius = MathHelper.Lerp(280f, 180f, impactT);
        }

        private void TriggerImpact()
        {
            _impactTriggered = true;
            _impactAge = 0f;

            SpawnImpactSparks(16);
            SpawnSmoke(6);
        }

        private void SpawnImpactSparks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FindDeadSpark();
                if (slot < 0)
                    return;

                Vector3 dir = RandomUnitVector3(0.1f, 0.95f);
                float speed = RandomRange(110f, 220f);
                float life = RandomRange(0.3f, 0.55f);

                _sparks[slot] = new SparkParticle
                {
                    Position = _targetPosition + RandomJitter(12f, 12f, 6f),
                    Velocity = dir * speed,
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    Scale = RandomRange(0.9f, 1.6f),
                    Kind = 1
                };
            }
        }

        private void SpawnSmoke(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FindDeadSpark();
                if (slot < 0)
                    return;

                Vector3 dir = RandomUnitVector3(0.2f, 0.9f);
                float speed = RandomRange(30f, 70f);
                float life = RandomRange(0.55f, 0.9f);

                _sparks[slot] = new SparkParticle
                {
                    Position = _targetPosition + RandomJitter(14f, 14f, 6f),
                    Velocity = dir * speed,
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    Scale = RandomRange(1.2f, 2.0f),
                    Kind = 2
                };
            }
        }

        private int FindDeadSpark()
        {
            for (int i = 0; i < MaxSparks; i++)
            {
                if (_sparks[i].Life <= 0f)
                    return i;
            }

            return -1;
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
                World.Terrain.RemoveDynamicLight(_ballLight);
                World.Terrain.RemoveDynamicLight(_impactLight);
                _lightsAdded = false;
            }

            base.Dispose();
        }
    }
}
