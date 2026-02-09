#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Rageful Blow (Fury Strike) visual effect built from original MU assets:
    /// EarthQuake (Skill/EarthQuake*.bmd), flashing (Skill/flashing.bmd), tail (Skill/tail.bmd).
    /// </summary>
    public sealed class RagefulBlowEffect : EffectObject
    {
        private const string WaveModelPath = "Skill/flashing.bmd";
        private const string TailModelPath = "Skill/tail.bmd";
        private const string EarthQuakeBasePath = "Skill/EarthQuake.bmd";

        private const string ExplosionTexturePath = "Effect/Explotion01.jpg";
        private const string JointSparkTexturePath = "Effect/Spark01.jpg";
        private const string SparkTexturePath = "Effect/Spark02.jpg";

        private const string SoundStrike1 = "Sound/eRageBlow_1.wav";
        private const string SoundStrike2 = "Sound/eRageBlow_2.wav";
        private const string SoundStrike3 = "Sound/eRageBlow_3.wav";

        private const float ImpactZOffset = 70f;
        private const int ExplosionFrameCount = 16;
        private const int ExplosionFrameColumns = 4;
        private const int ExplosionFrameRows = 4;
        private const float ExplosionLifeFrames = 20f;
        private const int MaxJointSparks = 12;
        private const int MaxSparks = 24;

        private readonly WalkerObject _caster;
        private readonly string[] _earthQuakePaths = new string[9];
        private bool _earthQuakePathsResolved;

        private Vector3 _startPosition;
        private Vector3 _position;
        private Vector3 _ownerAngleDeg;
        private Vector3 _baseAngleDeg;
        private Vector3 _headAngleDeg;
        private Vector3 _direction;
        private float _gravity;
        private float _lifeTimeFrames = 20f;
        private int _subType;
        private bool _initialized;
        private bool _impactTriggered;
        private bool _tailSpawned;
        private bool _secondaryBurstSpawned;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _explosionTexture = null!;
        private Texture2D _jointSparkTexture = null!;
        private Texture2D _sparkTexture = null!;
        private bool _texturesLoaded;
        private readonly DynamicLight _impactLight;
        private bool _lightsAdded;
        private float _time;

        private struct ExplosionParticle
        {
            public bool Active;
            public Vector3 Position;
            public float LifeFrames;
            public float Scale;
        }

        private struct JointSparkParticle
        {
            public bool Active;
            public Vector3 Position;
            public float LifeFrames;
            public float MaxLife;
            public float Scale;
            public float Rotation;
        }

        private struct SparkParticle
        {
            public bool Active;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Gravity;
            public float LifeFrames;
            public float MaxLife;
            public float Rotation;
            public float Scale;
        }

        private ExplosionParticle _explosion;
        private readonly JointSparkParticle[] _jointSparks = new JointSparkParticle[MaxJointSparks];
        private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];

        private abstract class RagefulSubEffect : ModelObject
        {
            protected float LifeTimeFrames;

            protected RagefulSubEffect(string path, float lifetimeFrames, bool additive, int blendMesh)
            {
                ModelPath = path;
                LifeTimeFrames = lifetimeFrames;
                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = additive ? BlendState.Additive : BlendState.NonPremultiplied;
                BlendMeshState = additive ? BlendState.Additive : BlendState.NonPremultiplied;
                BlendMesh = blendMesh;
                BlendMeshLight = 1.0f;
            }

            protected string ModelPath { get; }

            public override async Task Load()
            {
                Model = await BMDLoader.Instance.Prepare(ModelPath);
                await base.Load();
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);
                if (Status != GameControlStatus.Ready)
                    return;

                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                UpdateEffect(factor);

                LifeTimeFrames -= factor;
                if (LifeTimeFrames <= 0f)
                    RemoveSelf();
            }

            protected abstract void UpdateEffect(float factor);

            protected void RemoveSelf()
            {
                if (Parent != null)
                    Parent.Children.Remove(this);
                else
                    World?.RemoveObject(this);

                Dispose();
            }
        }

        private sealed class RagefulWaveEffect : RagefulSubEffect
        {
            public RagefulWaveEffect(string path, float lifetimeFrames)
                : base(path, lifetimeFrames, additive: true, blendMesh: 0)
            {
                Scale = 0.5f;
                BlendMeshLight = 1.5f;
            }

            protected override void UpdateEffect(float factor)
            {
                if (Scale > 2f)
                {
                    Scale += 0.1f * factor;
                    Position = new Vector3(Position.X - (1f * factor), Position.Y, Position.Z - (1.5f * factor));
                    BlendMeshLight = LifeTimeFrames / 30f;
                }
                else
                {
                    Scale += 1.2f * factor;
                    Position = new Vector3(Position.X - (1.2f * factor), Position.Y, Position.Z - (1.8f * factor));
                }
            }
        }

        private sealed class RagefulTailEffect : RagefulSubEffect
        {
            private float _gravity = 80f;

            public RagefulTailEffect(string path, float lifetimeFrames)
                : base(path, lifetimeFrames, additive: true, blendMesh: -2)
            {
                Scale = 1f;
                Angle = new Vector3(0f, 0f, MathHelper.ToRadians(45f));
            }

            protected override void UpdateEffect(float factor)
            {
                Position = new Vector3(Position.X, Position.Y, Position.Z - (_gravity * factor));
                _gravity += 60f * factor;
                BlendMeshLight = LifeTimeFrames / 20f;
            }
        }

        private sealed class RagefulEarthQuakeEffect : RagefulSubEffect
        {
            private readonly int _variant;

            public RagefulEarthQuakeEffect(string path, int variant, float lifetimeFrames)
                : base(path, lifetimeFrames, additive: true, blendMesh: -2)
            {
                _variant = variant;
            }

            protected override void UpdateEffect(float factor)
            {
                switch (_variant)
                {
                    case 1:
                    case 4:
                    case 7:
                        BlendMeshLight = (LifeTimeFrames * 0.1f) / 3f;
                        if (LifeTimeFrames < 10f)
                            Position = new Vector3(Position.X, Position.Y, Position.Z - (0.5f * factor));
                        break;
                    case 3:
                    case 6:
                        BlendMeshLight = (LifeTimeFrames * 0.1f) / 10f;
                        if (LifeTimeFrames < 13f)
                            Position = new Vector3(Position.X, Position.Y, Position.Z - (0.5f * factor));
                        break;
                    case 2:
                        if (LifeTimeFrames >= 10f)
                            BlendMeshLight = ((20f - LifeTimeFrames) * 0.1f);
                        else
                        {
                            if (LifeTimeFrames < 5f)
                                Position = new Vector3(Position.X, Position.Y, Position.Z - (0.5f * factor));
                            BlendMeshLight = LifeTimeFrames * 0.1f;
                        }
                        break;
                    case 5:
                        if (LifeTimeFrames >= 30f)
                            BlendMeshLight = ((40f - LifeTimeFrames) * 0.1f);
                        else
                        {
                            if (LifeTimeFrames < 15f)
                                Position = new Vector3(Position.X, Position.Y, Position.Z - (0.5f * factor));
                            BlendMeshLight = LifeTimeFrames * 0.1f;
                        }
                        break;
                    case 8:
                        if (LifeTimeFrames >= 30f)
                            BlendMeshLight = ((40f - LifeTimeFrames) * 0.1f);
                        else
                            BlendMeshLight = LifeTimeFrames * 0.1f;
                        if (LifeTimeFrames < 15f)
                            Position = new Vector3(Position.X, Position.Y, Position.Z - (0.5f * factor));
                        break;
                }
            }
        }

        public RagefulBlowEffect(WalkerObject caster, Vector3? targetPosition)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-320f, -320f, -80f),
                new Vector3(320f, 320f, 260f));

            _impactLight = new DynamicLight
            {
                Owner = this,
                Position = caster.WorldPosition.Translation,
                Color = new Vector3(1.0f, 0.6f, 0.25f),
                Radius = 280f,
                Intensity = 1.6f
            };

        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            await ResolveEarthQuakePaths();

            _ = await TextureLoader.Instance.Prepare(ExplosionTexturePath);
            _ = await TextureLoader.Instance.Prepare(JointSparkTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _explosionTexture = TextureLoader.Instance.GetTexture2D(ExplosionTexturePath) ?? GraphicsManager.Instance.Pixel;
            _jointSparkTexture = TextureLoader.Instance.GetTexture2D(JointSparkTexturePath) ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkTexturePath) ?? GraphicsManager.Instance.Pixel;
            _texturesLoaded = true;

            if (World?.Terrain != null && !_lightsAdded)
            {
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

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            if (!_initialized)
                InitializeBaseEffect();

            int lifeInt = (int)_lifeTimeFrames;

            if (lifeInt == 11 && !_impactTriggered)
                TriggerImpact();

            if (lifeInt == 13 && !_tailSpawned)
            {
                _tailSpawned = true;
                SpawnTailBursts();
            }

            if (lifeInt == 10 && !_secondaryBurstSpawned)
            {
                _secondaryBurstSpawned = true;
                SpawnSecondaryBursts();
            }

            if (lifeInt != 11 && lifeInt != 10)
                UpdateBaseMotion(FPSCounter.Instance.FPS_ANIMATION_FACTOR, _lifeTimeFrames);

            UpdateParticles(FPSCounter.Instance.FPS_ANIMATION_FACTOR);
            UpdateDynamicLight((float)gameTime.ElapsedGameTime.TotalSeconds);

            _lifeTimeFrames -= FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            if (_lifeTimeFrames <= 0f)
            {
                RemoveSelf();
                return;
            }

            Position = _position;
        }

        private async Task ResolveEarthQuakePaths()
        {
            if (_earthQuakePathsResolved)
                return;

            _earthQuakePaths[0] = EarthQuakeBasePath;
            for (int i = 1; i <= 8; i++)
            {
                string indexedPath = $"Skill/EarthQuake0{i}.bmd";
                string plainPath = $"Skill/EarthQuake{i}.bmd";
                if (await BMDLoader.Instance.AssestExist(indexedPath))
                {
                    _earthQuakePaths[i] = indexedPath;
                }
                else if (await BMDLoader.Instance.AssestExist(plainPath))
                {
                    _earthQuakePaths[i] = plainPath;
                }
                else
                {
                    _earthQuakePaths[i] = EarthQuakeBasePath;
                }
            }

            _earthQuakePathsResolved = true;
        }

        private void InitializeBaseEffect()
        {
            _ownerAngleDeg = ToDegrees(_caster.Angle);
            _baseAngleDeg = _ownerAngleDeg;
            _headAngleDeg = _ownerAngleDeg;

            _baseAngleDeg.Z += 330f;
            _headAngleDeg.X += 80f;
            _headAngleDeg.Z += 180f;

            _gravity = 50f;
            _subType = MuGame.Random.Next(100);

            _startPosition = _caster.WorldPosition.Translation;
            _position = _startPosition;
            _initialized = true;
        }

        private void TriggerImpact()
        {
            _impactTriggered = true;

            SoundController.Instance.PlayBuffer(SoundStrike1);

            Vector3 p = new Vector3(-25f, -80f, 0f);
            Matrix matrix = MathUtils.AngleMatrix(_ownerAngleDeg);
            Vector3 rotated = MathUtils.VectorRotate(p, matrix);
            _startPosition = _caster.WorldPosition.Translation + rotated;

            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_startPosition.X, _startPosition.Y);
                _startPosition.Z = groundZ + 25f + ImpactZOffset;
            }
            else
            {
                _startPosition.Z += ImpactZOffset;
            }

            SpawnImpactParticles();
            SpawnCoreBurst();
            SpawnPrimaryBursts();
        }

        private void UpdateDynamicLight(float dt)
        {
            _time += dt;
            float lifeAlpha = MathHelper.Clamp(_lifeTimeFrames / 20f, 0f, 1f);
            float pulse = 0.75f + 0.25f * MathF.Sin(_time * 10f);

            _impactLight.Position = _impactTriggered ? _startPosition : _position;
            _impactLight.Intensity = _impactTriggered ? 1.6f * lifeAlpha * pulse : 0f;
            _impactLight.Radius = MathHelper.Lerp(280f, 180f, 1f - lifeAlpha);
        }

        private void SpawnImpactParticles()
        {
            _explosion = new ExplosionParticle
            {
                Active = true,
                Position = _startPosition,
                LifeFrames = ExplosionLifeFrames,
                Scale = 0.5f
            };

            for (int j = 0; j < 8; j++)
            {
                if (!FPSCounter.Instance.RandFPSCheck(1))
                    continue;

                Vector3 angleDeg = _baseAngleDeg + new Vector3(MuGame.Random.Next(-60, 1), 0f, MuGame.Random.Next(90, 120));
                Vector3 position = _startPosition;
                position.X += MuGame.Random.Next(-10, 10);
                position.Y += MuGame.Random.Next(-10, 10);

                SpawnJointSpark(position, angleDeg);

                if (FPSCounter.Instance.RandFPSCheck(8))
                    SpawnSpark(position, angleDeg);
            }
        }

        private void SpawnJointSpark(Vector3 position, Vector3 angleDeg)
        {
            int slot = FindDeadJointSpark();
            if (slot < 0)
                return;

            float life = MuGame.Random.Next(8, 16);
            _jointSparks[slot] = new JointSparkParticle
            {
                Active = true,
                Position = position,
                LifeFrames = life,
                MaxLife = life,
                Scale = 2f,
                Rotation = MathHelper.ToRadians(angleDeg.Z)
            };
        }

        private void SpawnSpark(Vector3 position, Vector3 angleDeg)
        {
            int slot = FindDeadSpark();
            if (slot < 0)
                return;

            float scale = (MuGame.Random.Next(4, 8)) * 0.1f;
            float life = MuGame.Random.Next(24, 40);
            float gravity = MuGame.Random.Next(6, 22);
            float speed = (MuGame.Random.Next(20, 40)) * 0.1f;
            float angleZ = MathHelper.ToRadians(MuGame.Random.Next(0, 360));
            Vector3 velocity = new Vector3(MathF.Cos(angleZ) * speed, MathF.Sin(angleZ) * speed, 0f);

            _sparks[slot] = new SparkParticle
            {
                Active = true,
                Position = position,
                Velocity = velocity,
                Gravity = gravity,
                LifeFrames = life,
                MaxLife = life,
                Rotation = MathHelper.ToRadians(angleDeg.Z),
                Scale = scale
            };
        }

        private void SpawnCoreBurst()
        {
            Vector3 wavePos = _startPosition;
            wavePos.Z -= 15f * FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            AddModel(new RagefulWaveEffect(WaveModelPath, 15f)
            {
                Position = wavePos,
                Angle = Vector3.Zero
            });

            Vector3 quakePos = _startPosition;
            quakePos.Z -= 27f;
            AddModel(CreateEarthQuakeModel(3, quakePos, 1.5f));
            AddModel(CreateEarthQuakeModel(1, quakePos, 1.5f));
            AddModel(CreateEarthQuakeModel(2, quakePos, 1.5f));
        }

        private void SpawnPrimaryBursts()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(0f, MuGame.Random.Next(100, 250), 0f);
                Vector3 angleDeg = new Vector3(0f, 0f, _subType + (i * 72f));
                Matrix matrix = MathUtils.AngleMatrix(angleDeg);
                Vector3 rotated = MathUtils.VectorRotate(p, matrix);
                Vector3 pos = _startPosition + rotated;

                if (World?.Terrain != null)
                {
                    float groundZ = World.Terrain.RequestTerrainHeight(pos.X, pos.Y);
                    pos.Z = groundZ + 3f + ImpactZOffset;
                }
                else
                {
                    pos.Z += ImpactZOffset;
                }

                float scale = MuGame.Random.Next(40, 90) / 100f;
                float angleZ = 45f + (MuGame.Random.Next(30) - 15);

                AddModel(CreateEarthQuakeModel(4, pos, scale, angleZ));
                AddModel(CreateEarthQuakeModel(5, pos, scale, angleZ));
            }
        }

        private void SpawnSecondaryBursts()
        {
            Vector3[] pos = new Vector3[5];
            float[] ang = new float[5];

            for (int j = 0; j < 5; ++j)
            {
                pos[j] = _startPosition;
                ang[j] = 0f;
            }

            int count = 0;

            for (int j = 0; j < 4; ++j)
            {
                Vector3 p = new Vector3(0f, MuGame.Random.Next(85, 100), 0f);

                if (j >= 3)
                    count = MuGame.Random.Next();

                for (int i = 0; i < 5; ++i)
                {
                    int random = (count % 2) == 0
                        ? MuGame.Random.Next(50, 80)
                        : -MuGame.Random.Next(50, 80);

                    ang[i] += random;
                    float angleZ = ang[i] + (i * MuGame.Random.Next(62, 72));

                    Vector3 angleDeg = new Vector3(0f, 0f, angleZ);
                    Matrix matrix = MathUtils.AngleMatrix(angleDeg);
                    Vector3 rotated = MathUtils.VectorRotate(p, matrix);
                    pos[i] += rotated;

                    if (World?.Terrain != null)
                    {
                        float groundZ = World.Terrain.RequestTerrainHeight(pos[i].X, pos[i].Y);
                        pos[i].Z = groundZ + 3f + ImpactZOffset;
                    }
                    else
                    {
                        pos[i].Z += ImpactZOffset;
                    }

                    float finalAngleZ = angleZ + 270f;
                    AddModel(CreateEarthQuakeModel(7, pos[i], 1.0f, finalAngleZ));
                    AddModel(CreateEarthQuakeModel(8, pos[i], 1.0f, finalAngleZ));
                }

                count++;
            }

            SoundController.Instance.PlayBuffer(SoundStrike3);
            _lifeTimeFrames = 0f;
        }

        private void SpawnTailBursts()
        {
            Vector3 angleDeg = _ownerAngleDeg;
            Vector3 p = new Vector3(-25f, -40f, 0f);
            Matrix matrix = MathUtils.AngleMatrix(angleDeg);
            Vector3 rotated = MathUtils.VectorRotate(p, matrix);
            Vector3 pos = _position + rotated;

            Vector3 position;
            for (int i = 0; i < 4; ++i)
            {
                position = new Vector3(pos.X, pos.Y, pos.Z - (i * 50f));
                AddModel(new RagefulTailEffect(TailModelPath, 6f)
                {
                    Position = position,
                    Angle = new Vector3(0f, 0f, MathHelper.ToRadians(45f))
                });
            }

            pos.X += MuGame.Random.Next(20, 50);
            pos.Z += MuGame.Random.Next(-250, 250);

            for (int i = 0; i < 4; ++i)
            {
                position = new Vector3(pos.X, pos.Y, pos.Z - (i * 30f));
                AddModel(new RagefulTailEffect(TailModelPath, 6f)
                {
                    Position = position,
                    Angle = new Vector3(0f, 0f, MathHelper.ToRadians(45f))
                });
            }

            SoundController.Instance.PlayBuffer(SoundStrike2);
        }

        private RagefulEarthQuakeEffect CreateEarthQuakeModel(int variant, Vector3 position, float scale, float angleZDeg = 0f)
        {
            string path = _earthQuakePathsResolved ? _earthQuakePaths[Math.Clamp(variant, 0, 8)] : EarthQuakeBasePath;
            float lifetimeFrames = variant switch
            {
                1 => 35f,
                2 => 20f,
                3 => 35f,
                4 => 35f,
                5 => 40f,
                6 => 35f,
                7 => 40f,
                8 => 40f,
                _ => 35f
            };

            return new RagefulEarthQuakeEffect(path, variant, lifetimeFrames)
            {
                Position = position,
                Scale = scale,
                Angle = new Vector3(0f, 0f, MathHelper.ToRadians(angleZDeg))
            };
        }

        private void AddModel(RagefulSubEffect model)
        {
            if (World == null)
                return;

            World.Objects.Add(model);
            _ = model.Load();
        }

        private void UpdateBaseMotion(float factor, float lifeTime)
        {
            float count = lifeTime;
            float addAngle = 15f;
            if (lifeTime > 9f && lifeTime < 16f)
            {
                count = 12.5f;
                addAngle = 18f;
                if ((int)lifeTime == 15)
                    _gravity *= MathF.Pow(-1f, factor);
            }

            float angle = (20f - count) * addAngle;

            _baseAngleDeg.X += 80f * factor;
            _direction = new Vector3(0f, MathF.Sin(MathHelper.ToRadians(angle)) * 260f, 0f);

            _position = _startPosition;
            if (count < 12.5f || count > 12.5f)
                _gravity += 8f * factor;
            else
                _gravity -= 8f * factor;

            Matrix matrix = MathUtils.AngleMatrix(_headAngleDeg);
            Vector3 rotated = MathUtils.VectorRotate(_direction, matrix);
            _position += rotated;
            _position.Z += _gravity + 200f;
        }

        private void UpdateParticles(float factor)
        {
            if (_explosion.Active)
            {
                _explosion.LifeFrames -= factor;
                if (_explosion.LifeFrames <= 0f)
                    _explosion.Active = false;
            }

            for (int i = 0; i < MaxJointSparks; i++)
            {
                if (!_jointSparks[i].Active)
                    continue;

                _jointSparks[i].LifeFrames -= factor;
                if (_jointSparks[i].LifeFrames <= 0f)
                    _jointSparks[i].Active = false;
            }

            for (int i = 0; i < MaxSparks; i++)
            {
                if (!_sparks[i].Active)
                    continue;

                var spark = _sparks[i];
                spark.LifeFrames -= factor;
                if (spark.LifeFrames <= 0f)
                {
                    spark.Active = false;
                    _sparks[i] = spark;
                    continue;
                }

                spark.Position.Z += spark.Gravity * factor;
                spark.Gravity -= 2f * factor;
                spark.Position += spark.Velocity * factor;

                if (World?.Terrain != null)
                {
                    float groundZ = World.Terrain.RequestTerrainHeight(spark.Position.X, spark.Position.Y) + ImpactZOffset;
                    if (spark.Position.Z < groundZ)
                    {
                        spark.Position.Z = groundZ;
                        spark.Gravity = -spark.Gravity * 0.6f;
                        spark.LifeFrames -= 4f * factor;
                    }
                }

                _sparks[i] = spark;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!_texturesLoaded || _spriteBatch == null)
                return;

            using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthState))
            {
                DrawExplosion();
                DrawJointSparks();
                DrawSparks();
            }
        }

        private void DrawExplosion()
        {
            if (!_explosion.Active || _explosionTexture == null)
                return;

            if (_explosionTexture.Width < ExplosionFrameColumns || _explosionTexture.Height < ExplosionFrameRows)
            {
                DrawSprite(_explosionTexture, _explosion.Position, Color.White, 0f, new Vector2(_explosion.Scale));
                return;
            }

            float life = MathHelper.Clamp(_explosion.LifeFrames, 0f, ExplosionLifeFrames);
            int frame = Math.Clamp((int)((ExplosionLifeFrames - life) / 2f), 0, ExplosionFrameCount - 1);
            int frameX = frame % ExplosionFrameColumns;
            int frameY = frame / ExplosionFrameColumns;

            int frameWidth = _explosionTexture.Width / ExplosionFrameColumns;
            int frameHeight = _explosionTexture.Height / ExplosionFrameRows;

            Rectangle source = new Rectangle(frameX * frameWidth, frameY * frameHeight, frameWidth, frameHeight);
            DrawSprite(_explosionTexture, _explosion.Position, Color.White, 0f, new Vector2(_explosion.Scale), source);
        }

        private void DrawJointSparks()
        {
            for (int i = 0; i < MaxJointSparks; i++)
            {
                if (!_jointSparks[i].Active)
                    continue;

                float lifeRatio = _jointSparks[i].LifeFrames / MathF.Max(1f, _jointSparks[i].MaxLife);
                float alpha = MathHelper.Clamp(lifeRatio, 0f, 1f);
                DrawSprite(_jointSparkTexture, _jointSparks[i].Position, Color.White * alpha, _jointSparks[i].Rotation, new Vector2(_jointSparks[i].Scale));
            }
        }

        private void DrawSparks()
        {
            for (int i = 0; i < MaxSparks; i++)
            {
                if (!_sparks[i].Active)
                    continue;

                float lifeRatio = _sparks[i].LifeFrames / MathF.Max(1f, _sparks[i].MaxLife);
                float alpha = MathHelper.Clamp(lifeRatio, 0f, 1f);
                DrawSprite(_sparkTexture, _sparks[i].Position, Color.White * alpha, _sparks[i].Rotation, new Vector2(_sparks[i].Scale));
            }
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, Vector2 scale, Rectangle? source = null)
        {
            if (texture == null)
                return;

            var viewport = GraphicsDevice.Viewport;
            Vector3 projected = viewport.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            Vector2 finalScale = scale * baseScale;
            float depth = MathHelper.Clamp(projected.Z, 0f, 1f);

            Vector2 origin = source.HasValue
                ? new Vector2(source.Value.Width * 0.5f, source.Value.Height * 0.5f)
                : new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            _spriteBatch.Draw(
                texture,
                new Vector2(projected.X, projected.Y),
                source,
                color,
                rotation,
                origin,
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

        private int FindDeadJointSpark()
        {
            for (int i = 0; i < MaxJointSparks; i++)
            {
                if (!_jointSparks[i].Active)
                    return i;
            }
            return -1;
        }

        private int FindDeadSpark()
        {
            for (int i = 0; i < MaxSparks; i++)
            {
                if (!_sparks[i].Active)
                    return i;
            }
            return -1;
        }

        private static Vector3 ToDegrees(Vector3 radians)
        {
            return new Vector3(
                MathHelper.ToDegrees(radians.X),
                MathHelper.ToDegrees(radians.Y),
                MathHelper.ToDegrees(radians.Z));
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
                World.Terrain.RemoveDynamicLight(_impactLight);
                _lightsAdded = false;
            }

            base.Dispose();
        }
    }
}
