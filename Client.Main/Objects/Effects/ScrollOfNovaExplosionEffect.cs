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
    /// Scroll of Nova explosion effect (Skill ID 40).
    /// Renders an expanding torus-like energy wave moving away from the caster.
    /// </summary>
    public sealed class ScrollOfNovaExplosionEffect : EffectObject
    {
        private const string SpiritTexturePath = "Effect/JointSpirit01.jpg";
        private const string FlareTexturePath = "Effect/flare.jpg";
        private const string SoundNovaRelease = "Sound/eHellFire2_2.wav";

        private const float ImpactZOffset = 70f;
        private const float LifeFrames = 45f;
        private const float MaxNovaStage = 12f;
        private const int MaxTorusParticles = 600;

        private readonly WalkerObject _caster;
        private readonly byte _stage;
        private readonly DynamicLight _impactLight;
        private readonly TorusParticle[] _torusParticles = new TorusParticle[MaxTorusParticles];

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _spiritTexture = null!;
        private Texture2D _flareTexture = null!;

        private Vector3 _center;
        private int _torusCount;
        private float _lifeFrames = LifeFrames;
        private bool _initialized;
        private bool _lightAdded;
        private bool _soundPlayed;

        private struct TorusParticle
        {
            public Vector3 AxisDir;
            public float Radius;
            public float RadiusSpeed;
            public float TubeAngle;
            public float TubeSpeed;
            public float TubeRadius;
            public float Age;
            public float Lifetime;
            public float Size;
            public float Rotation;
            public float AlphaScale;
        }

        public ScrollOfNovaExplosionEffect(WalkerObject caster, Vector3 center, byte stage)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _center = center;
            _stage = (byte)Math.Clamp((int)stage, 0, (int)MaxNovaStage);

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-1500f, -1500f, -180f),
                new Vector3(1500f, 1500f, 520f));

            _impactLight = new DynamicLight
            {
                Owner = this,
                Position = center,
                Color = new Vector3(0.35f, 0.55f, 1f),
                Radius = 280f,
                Intensity = 1.35f
            };
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

            if (!_initialized)
                InitializeExplosion();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            float stageFactor = _stage / MaxNovaStage;

            _lifeFrames -= factor;
            float lifeFactor = MathHelper.Clamp(_lifeFrames / LifeFrames, 0f, 1f);
            float progress = 1f - lifeFactor;

            UpdateTorusParticles(dt);
            UpdateLight(lifeFactor, stageFactor, progress);

            if (_lifeFrames <= 0f && _torusCount == 0)
                RemoveSelf();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _spriteBatch == null)
                return;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthState))
                    DrawTorusParticles();
            }
            else
                DrawTorusParticles();
        }

        private void InitializeExplosion()
        {
            _initialized = true;

            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_center.X, _center.Y);
                _center = new Vector3(_center.X, _center.Y, groundZ + ImpactZOffset);
            }
            else
            {
                _center = new Vector3(_center.X, _center.Y, _center.Z + ImpactZOffset);
            }

            Position = _center;

            if (!_soundPlayed)
            {
                SoundController.Instance.PlayBuffer(SoundNovaRelease);
                _soundPlayed = true;
            }

            float shakePower = MathHelper.Lerp(2.4f, 4.6f, _stage / MaxNovaStage);
            Camera.Instance.Shake(shakePower, 0.35f, 20f);

            // One clear expanding ring, no additional center burst.
            SpawnTorusBand(progress: 0f, strong: true, 160);
        }

        private void SpawnTorusBand(float progress, bool strong, int segments)
        {
            if (segments <= 0)
                return;

            float stageFactor = _stage / MaxNovaStage;
            float baseRadius = MathHelper.Lerp(28f, 66f, stageFactor) + MathHelper.Lerp(0f, 120f, progress);
            float baseSpeed = MathHelper.Lerp(420f, 760f, stageFactor) + (strong ? 180f : 0f);
            float baseTube = MathHelper.Lerp(16f, 30f, stageFactor) * (strong ? 1.12f : 1f);

            for (int i = 0; i < segments; i++)
            {
                if (_torusCount >= MaxTorusParticles)
                    break;

                float azimuth = (MathHelper.TwoPi * i / segments) + RandomRange(-0.09f, 0.09f);
                Vector3 axis = new Vector3(MathF.Cos(azimuth), MathF.Sin(azimuth), 0f);
                float tubeSpeed = RandomRange(4.6f, 9.8f) * (MuGame.Random.Next(0, 2) == 0 ? -1f : 1f);

                _torusParticles[_torusCount++] = new TorusParticle
                {
                    AxisDir = axis,
                    Radius = baseRadius + RandomRange(-8f, 8f),
                    RadiusSpeed = baseSpeed + RandomRange(-28f, 46f),
                    TubeAngle = RandomRange(0f, MathHelper.TwoPi),
                    TubeSpeed = tubeSpeed,
                    TubeRadius = baseTube * RandomRange(0.75f, 1.2f),
                    Age = 0f,
                    Lifetime = strong ? RandomRange(0.85f, 1.30f) : RandomRange(0.75f, 1.10f),
                    Size = MathHelper.Lerp(24f, 48f, stageFactor) * (strong ? 1.1f : 1f) * RandomRange(0.85f, 1.15f),
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    AlphaScale = strong ? RandomRange(0.78f, 1f) : RandomRange(0.45f, 0.82f)
                };
            }
        }

        private void UpdateTorusParticles(float dt)
        {
            int i = 0;
            while (i < _torusCount)
            {
                ref var particle = ref _torusParticles[i];
                particle.Age += dt;
                if (particle.Age >= particle.Lifetime)
                {
                    _torusParticles[i] = _torusParticles[--_torusCount];
                    continue;
                }

                particle.Radius += particle.RadiusSpeed * dt;
                particle.RadiusSpeed *= 1f - MathHelper.Clamp(dt * 0.25f, 0f, 0.06f);
                particle.TubeAngle += particle.TubeSpeed * dt;
                particle.Rotation += dt * 2.2f;
                i++;
            }
        }

        private void DrawTorusParticles()
        {
            float lifeFactor = MathHelper.Clamp(_lifeFrames / LifeFrames, 0f, 1f);

            for (int i = 0; i < _torusCount; i++)
            {
                ref var particle = ref _torusParticles[i];
                float life = 1f - (particle.Age / particle.Lifetime);
                float alpha = life * particle.AlphaScale * MathHelper.Lerp(0.25f, 0.65f, lifeFactor);
                if (alpha <= 0.01f)
                    continue;

                Vector3 axis = particle.AxisDir;
                Vector3 torusBinormal = new Vector3(-axis.Y, axis.X, 0f);
                float cos = MathF.Cos(particle.TubeAngle);
                float sin = MathF.Sin(particle.TubeAngle);

                Vector3 worldPos = _center
                    + (axis * particle.Radius)
                    + (torusBinormal * (cos * particle.TubeRadius * 0.45f))
                    + new Vector3(0f, 0f, 88f + (sin * particle.TubeRadius));

                float size = particle.Size * (0.85f + (1f - life) * 0.28f);
                Color ringColor = new Color(0.10f * alpha, 0.24f * alpha, 0.85f * alpha, alpha);
                DrawSprite(_spiritTexture, worldPos, ringColor, particle.Rotation + 0.45f, size);
            }
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

        private void UpdateLight(float lifeFactor, float stageFactor, float progress)
        {
            if (World?.Terrain != null)
            {
                if (!_lightAdded)
                {
                    World.Terrain.AddDynamicLight(_impactLight);
                    _lightAdded = true;
                }
            }
            else if (_lightAdded)
            {
                World?.Terrain?.RemoveDynamicLight(_impactLight);
                _lightAdded = false;
            }

            _impactLight.Position = _center + new Vector3(0f, 0f, 90f);
            _impactLight.Radius = MathHelper.Lerp(210f, 460f, stageFactor) * (0.65f + progress * 0.5f);
            _impactLight.Intensity = MathHelper.Lerp(0.40f, 1.20f, stageFactor) * (0.35f + lifeFactor * 0.65f);
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

        private static float GetNowSeconds()
        {
            var gameTime = MuGame.Instance?.GameTime;
            if (gameTime != null)
                return (float)gameTime.TotalGameTime.TotalSeconds;

            return Environment.TickCount64 * 0.001f;
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
                World.Terrain.RemoveDynamicLight(_impactLight);
                _lightAdded = false;
            }

            base.Dispose();
        }
    }
}
