#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Ice visual effect (Skill ID 7), based on Main 5.2 behavior:
    /// one main ice effect and five smaller ice shards at impact.
    /// </summary>
    public sealed class ScrollOfIceEffect : EffectObject
    {
        private const string IceBaseName = "Ice";
        private const float TotalLifeFrames = 58f;
        private const float CoreLifeFrames = 48f;
        private const float ShardLifeFrames = 44f;
        private const int ShardCount = 5;

        private readonly Vector3 _center;

        private string _corePath = "Skill/Ice01.bmd";
        private string _smallPath = "Skill/Ice02.bmd";
        private bool _pathsResolved;
        private bool _spawned;
        private float _lifeFrames = TotalLifeFrames;
        private float _time;

        private readonly DynamicLight _freezeLight;
        private bool _lightAdded;

        public ScrollOfIceEffect(Vector3 center)
        {
            _center = center;
            Position = center;
            Angle = new Vector3(0f, 0f, RandomRange(0f, MathHelper.TwoPi));

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-160f, -160f, -120f),
                new Vector3(160f, 160f, 220f));

            _freezeLight = new DynamicLight
            {
                Owner = this,
                Position = center,
                Color = new Vector3(0.45f, 0.7f, 1f),
                Radius = 260f,
                Intensity = 1.25f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await ResolvePaths();

            if (World?.Terrain != null && !_lightAdded)
            {
                World.Terrain.AddDynamicLight(_freezeLight);
                _lightAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
                _ = Load();

            if (Status != GameControlStatus.Ready)
                return;

            if (!_spawned)
            {
                SpawnIceEffects();
                _spawned = true;
            }

            float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            _lifeFrames -= factor;
            _time += (float)gameTime.ElapsedGameTime.TotalSeconds;

            UpdateDynamicLight();

            if (_lifeFrames <= 0f)
                RemoveSelf();
        }

        private async Task ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _corePath = await ResolveModelPath(IceBaseName, 1, "Skill/Ice01.bmd", "Skill/Ice1.bmd", "Skill/Ice.bmd");
            _smallPath = await ResolveModelPath(IceBaseName, 2, _corePath, "Skill/Ice02.bmd", "Skill/Ice2.bmd");
            _pathsResolved = true;
        }

        private void SpawnIceEffects()
        {
            var core = new IceCoreModel(_corePath, CoreLifeFrames)
            {
                Position = Vector3.Zero,
                Angle = Vector3.Zero,
                Scale = 0.88f
            };
            Children.Add(core);
            _ = core.Load();

            for (int i = 0; i < ShardCount; i++)
            {
                Vector3 dir = RandomUnitVector3(0.15f, 0.9f);
                Vector3 velocity = new Vector3(
                    dir.X * RandomRange(2f, 5f),
                    dir.Y * RandomRange(2f, 5f),
                    RandomRange(2f, 6f));

                var shard = new IceShardModel(_smallPath, ShardLifeFrames, velocity)
                {
                    Position = new Vector3(
                        RandomRange(-24f, 24f),
                        RandomRange(-24f, 24f),
                        RandomRange(-8f, 48f)),
                    Angle = Vector3.Zero,
                    Scale = RandomRange(0.5f, 0.78f)
                };

                Children.Add(shard);
                _ = shard.Load();
            }
        }

        private void UpdateDynamicLight()
        {
            float alpha = MathHelper.Clamp(_lifeFrames / TotalLifeFrames, 0f, 1f);
            float pulse = 0.82f + 0.18f * MathF.Sin(_time * 13f);

            _freezeLight.Position = _center;
            _freezeLight.Intensity = 1.25f * alpha * pulse;
            _freezeLight.Radius = 220f + 40f * MathF.Sin(_time * 8f);
        }

        private static async Task<string> ResolveModelPath(string baseName, int index, params string[] fallbackCandidates)
        {
            string zeroPath = $"Skill/{baseName}0{index}.bmd";
            if (await BMDLoader.Instance.AssestExist(zeroPath))
                return zeroPath;

            string plainPath = $"Skill/{baseName}{index}.bmd";
            if (await BMDLoader.Instance.AssestExist(plainPath))
                return plainPath;

            for (int i = 0; i < fallbackCandidates.Length; i++)
            {
                string candidate = fallbackCandidates[i];
                if (await BMDLoader.Instance.AssestExist(candidate))
                    return candidate;
            }

            return zeroPath;
        }

        private static float RandomRange(float min, float max)
        {
            return (float)(MuGame.Random.NextDouble() * (max - min) + min);
        }

        private static Vector3 RandomUnitVector3(float zMin, float zMax)
        {
            float z = RandomRange(zMin, zMax);
            float theta = RandomRange(0f, MathHelper.TwoPi);
            float r = MathF.Sqrt(MathF.Max(0f, 1f - (z * z)));
            return new Vector3(r * MathF.Cos(theta), r * MathF.Sin(theta), z);
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
                World.Terrain.RemoveDynamicLight(_freezeLight);
                _lightAdded = false;
            }

            base.Dispose();
        }

        private sealed class IceCoreModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;
            private readonly float _initialLife;

            public IceCoreModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;
                _initialLife = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 3.8f;
                LightEnabled = true;
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
                _lifeFrames -= factor;
                BlendMeshLight = MathHelper.Clamp(_lifeFrames / _initialLife, 0f, 1f) * 1.2f;

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

        private sealed class IceShardModel : ModelObject
        {
            private readonly string _path;
            private Vector3 _velocity;
            private float _lifeFrames;
            private readonly float _initialLife;

            public IceShardModel(string path, float lifeFrames, Vector3 velocity)
            {
                _path = path;
                _velocity = velocity;
                _lifeFrames = lifeFrames;
                _initialLife = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 4.1f;
                LightEnabled = true;
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
                _lifeFrames -= factor;

                Position += _velocity * (0.09f * factor);
                _velocity = new Vector3(_velocity.X * 0.97f, _velocity.Y * 0.97f, _velocity.Z - (0.35f * factor));

                BlendMeshLight = MathHelper.Clamp(_lifeFrames / _initialLife, 0f, 1f);

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
