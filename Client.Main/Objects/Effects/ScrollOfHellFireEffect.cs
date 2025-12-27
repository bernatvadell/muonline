#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of HellFire visual effect (Skill ID 10) using original Circle models.
    /// </summary>
    public sealed class ScrollOfHellFireEffect : WorldObject
    {
        private const string CircleBaseName = "Circle";
        private const string StoneBaseName = "Stone";
        private const string SoundHellFire = "Sound/sHellFire.wav";
        private const float ImpactZOffset = 70f;

        private const float CircleLifeFrames = 45f;
        private const float CircleLightLifeFrames = 40f;

        private Vector3 _center;
        private readonly WalkerObject _caster;

        private string _circlePath = "Skill/Circle.bmd";
        private string _circleLightPath = "Skill/Circle.bmd";
        private readonly string[] _stonePaths = new string[2];
        private bool _pathsResolved;

        private HellFireCircleModel? _circle;
        private HellFireCircleLightModel? _circleLight;
        private readonly DynamicLight _impactLight;
        private bool _lightsAdded;
        private float _time;

        public ScrollOfHellFireEffect(WalkerObject caster, Vector3 center)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _center = center;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-320f, -320f, -80f),
                new Vector3(320f, 320f, 200f));

            _impactLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.55f, 0.25f),
                Radius = 300f,
                Intensity = 1.5f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await ResolvePaths();

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

            ForceInView();

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            if (_circle == null && World != null)
                SpawnCircleEffects();

            UpdateCircleLightStones();
            UpdateDynamicLight((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        private async Task ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _circlePath = await ResolveIndexedModelPath(CircleBaseName, 1, "Skill/Circle.bmd");
            _circleLightPath = await ResolveIndexedModelPath(CircleBaseName, 2, _circlePath);

            _stonePaths[0] = await ResolveIndexedModelPath(StoneBaseName, 1, "Skill/Stone.bmd");
            _stonePaths[1] = await ResolveIndexedModelPath(StoneBaseName, 2, _stonePaths[0]);

            _pathsResolved = true;
        }

        private void SpawnCircleEffects()
        {
            if (World == null)
                return;

            if (World.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_center.X, _center.Y);
                _center = new Vector3(_center.X, _center.Y, groundZ + ImpactZOffset);
            }
            else
            {
                _center = new Vector3(_center.X, _center.Y, _center.Z + ImpactZOffset);
            }

            SoundController.Instance.PlayBuffer(SoundHellFire);

            _circle = new HellFireCircleModel(_circlePath, CircleLifeFrames)
            {
                Position = _center,
                Angle = Vector3.Zero,
                Scale = 1f
            };

            _circleLight = new HellFireCircleLightModel(_circleLightPath, CircleLightLifeFrames)
            {
                Position = _center,
                Angle = Vector3.Zero,
                Scale = 1f
            };

            World.Objects.Add(_circle);
            World.Objects.Add(_circleLight);
            _ = _circle.Load();
            _ = _circleLight.Load();
        }

        private void UpdateCircleLightStones()
        {
            if (_circleLight == null || _circleLight.Status != GameControlStatus.Ready)
                return;

            if (!FPSCounter.Instance.RandFPSCheck(4))
                return;

            Vector3 p = new Vector3(0f, MuGame.Random.Next(0, 300), 0f);
            Vector3 angleDeg = new Vector3(0f, 0f, MuGame.Random.Next(0, 360));
            Matrix matrix = MathUtils.AngleMatrix(angleDeg);
            Vector3 rotated = MathUtils.VectorRotate(p, matrix);
            Vector3 position = _center + rotated;

            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(position.X, position.Y);
                position.Z = groundZ + ImpactZOffset;
            }
            else
            {
                position.Z += ImpactZOffset;
            }

            string stonePath = _stonePaths[MuGame.Random.Next(0, _stonePaths.Length)];
            var stone = new HellFireStoneModel(stonePath, 40f)
            {
                Position = position,
                Angle = Vector3.Zero,
                Scale = 0.9f
            };

            World?.Objects.Add(stone);
            _ = stone.Load();
        }

        private void UpdateDynamicLight(float dt)
        {
            if (World?.Terrain == null)
                return;

            _time += dt;

            bool active = (_circle != null && _circle.Status == GameControlStatus.Ready)
                || (_circleLight != null && _circleLight.Status == GameControlStatus.Ready);

            float pulse = 0.8f + 0.2f * MathF.Sin(_time * 10f);
            _impactLight.Position = _center;
            _impactLight.Intensity = active ? 1.4f * pulse : 0f;
            _impactLight.Radius = 260f + 40f * MathF.Sin(_time * 6f);
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

        private sealed class HellFireCircleModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;

            public HellFireCircleModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = 0;
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
                BlendMeshLight = _lifeFrames * 0.1f;
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

        private sealed class HellFireCircleLightModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;

            public HellFireCircleLightModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = 0;
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
                if (_lifeFrames >= 30f)
                    BlendMeshLight = (40f - _lifeFrames) * 0.1f;
                else
                    BlendMeshLight = _lifeFrames * 0.1f;

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

        private sealed class HellFireStoneModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;
            private float _gravity;

            public HellFireStoneModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = false;
                DepthState = DepthStencilState.DepthRead;
                _gravity = 0f;
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

                Position = new Vector3(Position.X, Position.Y, Position.Z + (_gravity * factor));
                _gravity += 0.5f * factor;
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
