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
    /// Fire Ball visual effect: red fire orb with tail, sparks, and dynamic lighting.
    /// </summary>
    public sealed class ScrollOfFireBallEffect : EffectObject
    {
        private const string CoreTexturePath = "Effect/flare01.jpg";
        private const string GlowTexturePath = "Effect/flare.jpg";
        private const string TailTexturePath = "Effect/firehik01.jpg";
        private const string SparkTexturePath = "Effect/Spark03.jpg";

        private const int MaxSparks = 48;
        private const float DefaultSpeed = 1800f;
        private const float ImpactDuration = 0.22f;
        private const float TailLength = 220f;
        private const int TailSegments = 6;
        private const float SparkSpawnInterval = 0.015f;
        private const float SparkGravity = 380f;
        private const float VisualScale = 12f;

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
        private Texture2D _coreTexture = null!;
        private Texture2D _glowTexture = null!;
        private Texture2D _tailTexture = null!;
        private Texture2D _sparkTexture = null!;

        private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];

        private readonly DynamicLight _ballLight;
        private readonly DynamicLight _impactLight;
        private bool _lightsAdded;
        private readonly FireBallCoreModel _coreModel;

        private readonly Color _coreColor = new(1f, 0.25f, 0.1f, 1f);
        private readonly Color _glowColor = new(1f, 0.35f, 0.15f, 0.85f);
        private readonly Color _tailColor = new(1f, 0.55f, 0.2f, 0.9f);
        private readonly Color _sparkColor = new(1f, 0.45f, 0.18f, 1f);
        private readonly Color _smokeColor = new(0.35f, 0.2f, 0.2f, 0.7f);

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

        public ScrollOfFireBallEffect(Vector3 startPosition, Vector3 targetPosition, float speed = DefaultSpeed)
        {
            _startPosition = startPosition;
            _targetPosition = targetPosition;
            _currentPosition = startPosition;

            Vector3 delta = targetPosition - startPosition;
            float distance = delta.Length();
            if (distance > 0.001f)
            {
                _direction = delta / distance;
            }
            else
            {
                _direction = Vector3.UnitY;
            }

            float effectiveSpeed = MathHelper.Clamp(speed, 600f, 4200f);
            _duration = MathHelper.Clamp(distance / MathF.Max(1f, effectiveSpeed), 0.18f, 1.2f);
            _totalDuration = _duration + ImpactDuration;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-160f, -160f, -120f),
                new Vector3(160f, 160f, 160f));

            _ballLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.35f, 0.15f),
                Radius = 200f,
                Intensity = 1.2f
            };

            _impactLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.4f, 0.2f),
                Radius = 320f,
                Intensity = 0f
            };

            _coreModel = new FireBallCoreModel
            {
                Scale = 1.1f,
                Position = Vector3.Zero
            };
            Children.Add(_coreModel);
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(CoreTexturePath);
            _ = await TextureLoader.Instance.Prepare(GlowTexturePath);
            _ = await TextureLoader.Instance.Prepare(TailTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _coreTexture = TextureLoader.Instance.GetTexture2D(CoreTexturePath) ?? GraphicsManager.Instance.Pixel;
            _glowTexture = TextureLoader.Instance.GetTexture2D(GlowTexturePath) ?? GraphicsManager.Instance.Pixel;
            _tailTexture = TextureLoader.Instance.GetTexture2D(TailTexturePath) ?? GraphicsManager.Instance.Pixel;
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
            {
                _ = Load();
            }

            if (Status != GameControlStatus.Ready)
                return;

            ForceInView();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;
            _spin += dt * 6.5f;

            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.1f, 0f, 1f) : 1f;
            _coreModel.Alpha = travelAlpha;
            _coreModel.Angle = new Vector3(0f, 0f, _spin);
            _coreModel.Light = new Vector3(1f, 0.35f, 0.15f) * (0.7f + 0.3f * MathF.Sin(_time * 16f));
            _coreModel.BlendMeshLight = 0.75f + 0.25f * MathF.Sin(_time * 12f);

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
                {
                    TriggerImpact();
                }
                _impactAge += dt;
            }

            UpdateSparks(dt);
            UpdateDynamicLights();

            Position = _currentPosition;

            if (_time >= _totalDuration)
            {
                RemoveSelf();
            }
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
            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.1f, 0f, 1f) : 1f;
            float pulse = 0.85f + 0.15f * MathF.Sin(_time * 18f);

            if (travelAlpha > 0f)
            {
                DrawTail(travelAlpha);
                DrawBall(travelAlpha * pulse);
            }

            if (_impactTriggered)
            {
                DrawImpactFlash();
            }

            DrawSparks();
        }

        private void DrawBall(float alpha)
        {
            float orbit = _spin * 1.8f;
            float orbitRadius = 18f + 6f * MathF.Sin(_spin * 1.5f);
            Vector3 orbitOffset = new Vector3(MathF.Cos(orbit) * orbitRadius, MathF.Sin(orbit) * orbitRadius, MathF.Sin(orbit * 1.3f) * 6f);

            DrawSprite(_coreTexture, _currentPosition, _coreColor * alpha, _spin * 2.2f, new Vector2(1.1f, 1.1f), VisualScale);
            DrawSprite(_glowTexture, _currentPosition, _glowColor * (alpha * 0.75f), -_spin * 1.4f, new Vector2(2.0f, 2.0f), VisualScale);
            DrawSprite(_coreTexture, _currentPosition + orbitOffset, _coreColor * (alpha * 0.55f), orbit, new Vector2(0.7f, 0.7f), VisualScale);
            DrawSprite(_coreTexture, _currentPosition - orbitOffset, _coreColor * (alpha * 0.55f), -orbit, new Vector2(0.7f, 0.7f), VisualScale);
        }

        private void DrawTail(float alpha)
        {
            Vector3 tailDir = -_direction;
            for (int i = 1; i <= TailSegments; i++)
            {
                float t = i / (float)TailSegments;
                Vector3 pos = _currentPosition + tailDir * (TailLength * t);
                float scale = MathHelper.Lerp(1.1f, 0.3f, t);
                float fade = (1f - t) * alpha;
                DrawSprite(_tailTexture, pos, _tailColor * fade, _spin * 1.3f + t, new Vector2(scale, scale), VisualScale);
            }
        }

        private void DrawImpactFlash()
        {
            float impactT = MathHelper.Clamp(_impactAge / ImpactDuration, 0f, 1f);
            float flashAlpha = MathF.Pow(1f - impactT, 1.6f);
            float flashScale = MathHelper.Lerp(1.2f, 3.6f, impactT);

            DrawSprite(_coreTexture, _targetPosition, _coreColor * flashAlpha, _impactAge * 6f, new Vector2(flashScale, flashScale), VisualScale);
            DrawSprite(_glowTexture, _targetPosition, _glowColor * (flashAlpha * 0.6f), _impactAge * 2.2f, new Vector2(flashScale * 1.5f, flashScale * 1.5f), VisualScale);
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
                float scale = _sparks[i].Scale * (0.7f + 0.3f * lifeRatio);

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
                    Vector3 lateral = RandomUnitVector3(-0.2f, 0.25f) * RandomRange(10f, 55f);
                    Vector3 velocity = (-_direction * RandomRange(80f, 160f)) + lateral;
                    float life = RandomRange(0.2f, 0.4f);

                    _sparks[slot] = new SparkParticle
                    {
                        Position = _currentPosition + RandomJitter(10f, 10f, 6f),
                        Velocity = velocity,
                        Life = life,
                        MaxLife = life,
                        Rotation = RandomRange(0f, MathHelper.TwoPi),
                        Scale = RandomRange(0.6f, 1.1f),
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
                spark.Rotation += dt * 4.5f;
                _sparks[i] = spark;
            }
        }

        private void UpdateDynamicLights()
        {
            if (World?.Terrain == null)
                return;

            float travelAlpha = _impactTriggered ? MathHelper.Clamp(1f - _impactAge / 0.1f, 0f, 1f) : 1f;
            float pulse = 0.75f + 0.25f * MathF.Sin(_time * 16f);

            _ballLight.Position = _currentPosition;
            _ballLight.Intensity = 1.1f * travelAlpha * pulse;
            _ballLight.Radius = 190f + 20f * MathF.Sin(_time * 8f);

            float impactT = _impactTriggered ? MathHelper.Clamp(_impactAge / ImpactDuration, 0f, 1f) : 0f;
            float impactAlpha = _impactTriggered ? MathF.Pow(1f - impactT, 1.3f) : 0f;

            _impactLight.Position = _targetPosition;
            _impactLight.Intensity = 1.8f * impactAlpha;
            _impactLight.Radius = MathHelper.Lerp(320f, 200f, impactT);
        }

        private void TriggerImpact()
        {
            _impactTriggered = true;
            _impactAge = 0f;

            SpawnImpactSparks(18);
            SpawnSmoke(8);
        }

        private void SpawnImpactSparks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FindDeadSpark();
                if (slot < 0)
                    return;

                Vector3 dir = RandomUnitVector3(0.1f, 0.95f);
                float speed = RandomRange(120f, 260f);
                float life = RandomRange(0.35f, 0.65f);

                _sparks[slot] = new SparkParticle
                {
                    Position = _targetPosition + RandomJitter(14f, 14f, 8f),
                    Velocity = dir * speed,
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    Scale = RandomRange(1.0f, 1.8f),
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
                float speed = RandomRange(35f, 80f);
                float life = RandomRange(0.6f, 1.0f);

                _sparks[slot] = new SparkParticle
                {
                    Position = _targetPosition + RandomJitter(18f, 18f, 8f),
                    Velocity = dir * speed,
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    Scale = RandomRange(1.4f, 2.4f),
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
