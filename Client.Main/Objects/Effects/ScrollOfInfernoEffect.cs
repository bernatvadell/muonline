#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Inferno visual effect (Skill ID 14) based on original MU client behavior.
    /// </summary>
    public sealed class ScrollOfInfernoEffect : EffectObject
    {
        private const string InfernoModelPath = "Skill/inferno01.bmd";
        private const string InfernoTexturePath = "Effect/inferno.jpg";
        private const string ExplosionTexturePath = "Effect/Explotion01.jpg";
        private const string SparkTexturePath = "Effect/Spark03.jpg";
        private const string StoneBaseName = "Stone";
        private const string SoundInferno = "Sound/sFlame.wav";

        private const float RingRadius = 220f;
        private const float ImpactZOffset = 0f;
        private const float BombZOffset = 80f;
        private const float CoreLifeFrames = 15f;
        private const float BurstLifeFrames = 20f;
        private const int BurstCount = 8;
        private const int SparksPerBurst = 20;
        private const int MaxSparks = BurstCount * SparksPerBurst;

        private readonly WalkerObject _caster;
        private Vector3 _center;
        private bool _initialized;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _infernoTexture = null!;
        private Texture2D _explosionTexture = null!;
        private Texture2D _sparkTexture = null!;
        private bool _texturesLoaded;

        private readonly string[] _stonePaths = new string[2];
        private bool _stonePathsResolved;

        private InfernoCoreModel? _core;

        private readonly Burst[] _bursts = new Burst[BurstCount];
        private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];

        private float _lifeFrames = CoreLifeFrames;
        private float _time;
        private readonly DynamicLight _coreLight;
        private bool _lightsAdded;

        public ScrollOfInfernoEffect(WalkerObject caster, Vector3 center)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _center = center;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-350f, -350f, -80f),
                new Vector3(350f, 350f, 260f));

            _coreLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.35f, 0.1f),
                Radius = 320f,
                Intensity = 2.1f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            await ResolveStonePaths();

            _ = await TextureLoader.Instance.Prepare(InfernoTexturePath);
            _ = await TextureLoader.Instance.Prepare(ExplosionTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _infernoTexture = TextureLoader.Instance.GetTexture2D(InfernoTexturePath) ?? GraphicsManager.Instance.Pixel;
            _explosionTexture = TextureLoader.Instance.GetTexture2D(ExplosionTexturePath) ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkTexturePath) ?? GraphicsManager.Instance.Pixel;
            _texturesLoaded = true;

            if (World?.Terrain != null && !_lightsAdded)
            {
                World.Terrain.AddDynamicLight(_coreLight);
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

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            if (!_initialized)
                InitializeEffect();

            float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            _time += (float)gameTime.ElapsedGameTime.TotalSeconds;

            UpdateBursts(factor);
            UpdateSparks(factor);
            UpdateDynamicLight();

            _lifeFrames -= factor;
            if (_lifeFrames <= 0f)
            {
                RemoveSelf();
                return;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!_texturesLoaded || !Visible)
                return;

            if (!Helpers.SpriteBatchScope.BatchIsBegun)
            {
                using (new Helpers.SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState, SamplerState.PointClamp, DepthState))
                {
                    DrawBursts();
                    DrawSparks();
                }
            }
            else
            {
                DrawBursts();
                DrawSparks();
            }
        }

        private void InitializeEffect()
        {
            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_center.X, _center.Y);
                _center = new Vector3(_center.X, _center.Y, groundZ + ImpactZOffset);
            }

            SoundController.Instance.PlayBuffer(SoundInferno);

            _core = new InfernoCoreModel(InfernoModelPath, CoreLifeFrames)
            {
                Position = _center,
                Angle = Vector3.Zero,
                Scale = 0.9f
            };

            World?.Objects.Add(_core);
            _ = _core.Load();

            SpawnBursts();
            _initialized = true;
        }

        private void SpawnBursts()
        {
            for (int i = 0; i < BurstCount; i++)
            {
                Vector3 angleDeg = new Vector3(0f, 0f, i * 45f);
                Vector3 p = new Vector3(0f, -RingRadius, 0f);
                Matrix matrix = MathUtils.AngleMatrix(angleDeg);
                Vector3 rotated = MathUtils.VectorRotate(p, matrix);
                Vector3 pos = _center + rotated;

                if (World?.Terrain != null)
                {
                    float groundZ = World.Terrain.RequestTerrainHeight(pos.X, pos.Y);
                    pos.Z = groundZ + ImpactZOffset;
                }

                pos.Z += BombZOffset;

                _bursts[i] = new Burst
                {
                    Active = true,
                    Position = pos,
                    LifeFrames = BurstLifeFrames,
                    MaxLife = BurstLifeFrames,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    RotationSpeed = RandomRange(-0.25f, 0.25f),
                    Scale = RandomRange(0.8f, 1.15f)
                };

                SpawnBurstSparks(pos);
                SpawnBurstStones(pos);
            }
        }

        private void SpawnBurstSparks(Vector3 position)
        {
            for (int i = 0; i < SparksPerBurst; i++)
            {
                int slot = FindDeadSpark();
                if (slot < 0)
                    return;

                Vector3 dir = RandomUnitVector3(0.1f, 0.8f);
                float speed = RandomRange(60f, 140f);

                _sparks[slot] = new SparkParticle
                {
                    Active = true,
                    Position = position,
                    Velocity = dir * speed,
                    LifeFrames = RandomRange(8f, 14f),
                    MaxLife = RandomRange(8f, 14f),
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    RotationSpeed = RandomRange(-0.4f, 0.4f),
                    Scale = RandomRange(0.5f, 0.9f),
                    Color = new Color(1f, 0.7f, 0.4f, 0.9f)
                };
            }
        }

        private void SpawnBurstStones(Vector3 position)
        {
            for (int i = 0; i < 2; i++)
            {
                string stonePath = _stonePaths[MuGame.Random.Next(0, _stonePaths.Length)];
                Vector3 velocity = new Vector3(
                    RandomRange(-6f, 6f),
                    RandomRange(-6f, 6f),
                    RandomRange(6f, 12f));

                var stone = new InfernoStoneModel(stonePath, 18f, velocity)
                {
                    Position = position,
                    Angle = Vector3.Zero,
                    Scale = RandomRange(0.7f, 1.0f)
                };

                World?.Objects.Add(stone);
                _ = stone.Load();
            }
        }

        private void UpdateBursts(float factor)
        {
            for (int i = 0; i < _bursts.Length; i++)
            {
                if (!_bursts[i].Active)
                    continue;

                var burst = _bursts[i];
                burst.LifeFrames -= factor;
                burst.Rotation += burst.RotationSpeed * factor;

                if (burst.LifeFrames <= 0f)
                    burst.Active = false;

                _bursts[i] = burst;
            }
        }

        private void UpdateSparks(float factor)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (!_sparks[i].Active)
                    continue;

                var spark = _sparks[i];
                spark.LifeFrames -= factor;
                spark.Position += spark.Velocity * (0.03f * factor);
                spark.Velocity = new Vector3(spark.Velocity.X * 0.95f, spark.Velocity.Y * 0.95f, spark.Velocity.Z - 1.4f * factor);
                spark.Rotation += spark.RotationSpeed * factor;

                if (spark.LifeFrames <= 0f)
                    spark.Active = false;

                _sparks[i] = spark;
            }
        }

        private void UpdateDynamicLight()
        {
            if (World?.Terrain == null)
                return;

            float lifeAlpha = MathHelper.Clamp(_lifeFrames / CoreLifeFrames, 0f, 1f);
            float pulse = 0.8f + 0.2f * MathF.Sin(_time * 12f);

            _coreLight.Position = _center;
            _coreLight.Intensity = 2.1f * lifeAlpha * pulse;
            _coreLight.Radius = MathHelper.Lerp(360f, 240f, 1f - lifeAlpha);
        }

        private void DrawBursts()
        {
            for (int i = 0; i < _bursts.Length; i++)
            {
                if (!_bursts[i].Active)
                    continue;

                float life = MathHelper.Clamp(_bursts[i].LifeFrames, 0f, _bursts[i].MaxLife);
                float t = 1f - (life / _bursts[i].MaxLife);
                float alpha = MathHelper.Clamp(1f - t, 0f, 1f);
                Color color = new Color(1f, 0.75f, 0.45f, alpha);

                DrawSprite(_infernoTexture, _bursts[i].Position, color, _bursts[i].Rotation, new Vector2(2.0f, 2.0f), _bursts[i].Scale);
                DrawExplosionSprite(_bursts[i].Position, t, alpha);
            }
        }

        private void DrawExplosionSprite(Vector3 position, float t, float alpha)
        {
            if (_explosionTexture == null)
                return;

            const int frameColumns = 4;
            const int frameRows = 4;
            const int frameCount = 16;

            int frame = Math.Clamp((int)(t * (frameCount - 1)), 0, frameCount - 1);
            int frameX = frame % frameColumns;
            int frameY = frame / frameColumns;

            int frameWidth = _explosionTexture.Width / frameColumns;
            int frameHeight = _explosionTexture.Height / frameRows;
            Rectangle source = new Rectangle(frameX * frameWidth, frameY * frameHeight, frameWidth, frameHeight);

            DrawSprite(_explosionTexture, position, new Color(1f, 0.7f, 0.4f, alpha), 0f, new Vector2(2.4f, 2.4f), 1f, source);
        }

        private void DrawSparks()
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (!_sparks[i].Active)
                    continue;

                float life = MathHelper.Clamp(_sparks[i].LifeFrames, 0f, _sparks[i].MaxLife);
                float alpha = MathHelper.Clamp(life / _sparks[i].MaxLife, 0f, 1f);
                Color color = _sparks[i].Color * alpha;

                DrawSprite(_sparkTexture, _sparks[i].Position, color, _sparks[i].Rotation, new Vector2(0.6f, 0.6f), _sparks[i].Scale);
            }
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, Vector2 scale, float scaleMultiplier = 1f, Rectangle? source = null)
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
                source,
                color,
                rotation,
                new Vector2((source?.Width ?? texture.Width) * 0.5f, (source?.Height ?? texture.Height) * 0.5f),
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

        private async Task ResolveStonePaths()
        {
            if (_stonePathsResolved)
                return;

            _stonePaths[0] = await ResolveIndexedModelPath(StoneBaseName, 1, "Skill/Stone.bmd");
            _stonePaths[1] = await ResolveIndexedModelPath(StoneBaseName, 2, _stonePaths[0]);
            _stonePathsResolved = true;
        }

        private static async Task<string> ResolveIndexedModelPath(string baseName, int index, string fallback)
        {
            string zeroPath = $"Skill/{baseName}0{index}.bmd";
            if (await BMDLoader.Instance.AssestExist(zeroPath))
                return zeroPath;

            string plainPath = $"Skill/{baseName}{index}.bmd";
            if (await BMDLoader.Instance.AssestExist(plainPath))
                return plainPath;

            if (await BMDLoader.Instance.AssestExist(fallback))
                return fallback;

            return zeroPath;
        }

        private int FindDeadSpark()
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (!_sparks[i].Active)
                    return i;
            }

            return -1;
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

        private static Vector3 RandomUnitVector3(float zMin, float zMax)
        {
            float z = RandomRange(zMin, zMax);
            float theta = RandomRange(0f, MathHelper.TwoPi);
            float r = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
            return new Vector3(r * MathF.Cos(theta), r * MathF.Sin(theta), z);
        }

        public override void Dispose()
        {
            if (_lightsAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_coreLight);
                _lightsAdded = false;
            }

            base.Dispose();
        }

        private struct Burst
        {
            public bool Active;
            public Vector3 Position;
            public float LifeFrames;
            public float MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public float Scale;
        }

        private struct SparkParticle
        {
            public bool Active;
            public Vector3 Position;
            public Vector3 Velocity;
            public float LifeFrames;
            public float MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public float Scale;
            public Color Color;
        }

        private sealed class InfernoCoreModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;

            public InfernoCoreModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                Light = new Vector3(0.8f, 0.8f, 0.8f);
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = -2;
            }

            public override async Task Load()
            {
                Model = await BMDLoader.Instance.Prepare(_path);
                await base.Load();
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);
                if (Status != GameControlStatus.Ready)
                    return;

                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                BlendMeshLight = _lifeFrames / 20f;
                _lifeFrames -= factor;

                if (_lifeFrames <= 0f)
                    RemoveSelf();
            }

            private void RemoveSelf()
            {
                if (Parent != null)
                    Parent.Children.Remove(this);
                else
                    World?.RemoveObject(this);

                Dispose();
            }
        }

        private sealed class InfernoStoneModel : ModelObject
        {
            private readonly string _path;
            private Vector3 _velocity;
            private float _lifeFrames;

            public InfernoStoneModel(string path, float lifeFrames, Vector3 initialVelocity)
            {
                _path = path;
                _lifeFrames = lifeFrames;
                _velocity = initialVelocity;

                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = false;
                DepthState = DepthStencilState.DepthRead;
            }

            public override async Task Load()
            {
                Model = await BMDLoader.Instance.Prepare(_path);
                await base.Load();
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);
                if (Status != GameControlStatus.Ready)
                    return;

                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                Position += _velocity * (0.08f * factor);
                _velocity = new Vector3(_velocity.X * 0.98f, _velocity.Y * 0.98f, _velocity.Z - 1.2f * factor);
                _lifeFrames -= factor;

                if (_lifeFrames <= 0f)
                    RemoveSelf();
            }

            private void RemoveSelf()
            {
                if (Parent != null)
                    Parent.Children.Remove(this);
                else
                    World?.RemoveObject(this);

                Dispose();
            }
        }
    }
}
