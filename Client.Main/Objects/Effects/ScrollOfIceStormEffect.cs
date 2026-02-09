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
    /// Scroll of Ice Storm visual effect (Skill ID 39) using Blizzard models.
    /// Creates 10 cascading falling ice shards with particle trails and ground debris.
    /// </summary>
    public sealed class ScrollOfIceStormEffect : EffectObject
    {
        private const string BlizzardBaseName = "Blizzard";
        private const string IceStoneBaseName = "ice_stone0";
        private const string IceImpactBaseName = "Ice";
        private const string SoundIceStorm1 = "Sound/sSuddenIce1.wav";
        private const string SoundIceStorm2 = "Sound/sSuddenIce2.wav";
        private const float ImpactZOffset = 80f;
        private const int BlizzardCount = 20;
        private const float EffectLifeFrames = 120f;

        private Vector3 _center;
        private readonly WalkerObject _caster;

        private string _blizzardPath = "Skill/Blizzard.bmd";
        private string _iceImpactPath = "Skill/Ice2.bmd";
        private readonly string[] _iceStonePaths = new string[4];
        private bool _pathsResolved;

        private readonly DynamicLight _impactLight;
        private bool _lightsAdded;
        private float _time;
        private bool _blizzardsSpawned;
        private int _impactCount;
        private float _remainingLifeFrames;

        public ScrollOfIceStormEffect(WalkerObject caster, Vector3 center)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _center = center;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-400f, -400f, -80f),
                new Vector3(400f, 400f, 800f));

            _impactLight = new DynamicLight
            {
                Owner = this,
                Position = center,
                Color = new Vector3(0.4f, 0.6f, 1.0f),
                Radius = 275f,
                Intensity = 1.1f
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

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            if (!_blizzardsSpawned && World != null)
            {
                SpawnBlizzards();
                _blizzardsSpawned = true;
                _remainingLifeFrames = EffectLifeFrames;
            }

            if (_blizzardsSpawned)
            {
                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                _remainingLifeFrames -= factor;

                if (_remainingLifeFrames <= 0f)
                {
                    RemoveSelf();
                    return;
                }
            }

            UpdateDynamicLight((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        private async Task ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _blizzardPath = await ResolveIndexedModelPath(BlizzardBaseName, 1, "Skill/Blizzard.bmd");
            _iceImpactPath = await ResolveIndexedModelPath(IceImpactBaseName, 2, "Skill/Ice2.bmd");

            for (int i = 0; i < 4; i++)
            {
                _iceStonePaths[i] = await ResolveIndexedModelPath($"{IceStoneBaseName}{i}", 0, $"Effect/ice_stone0{i}.bmd");
            }

            _pathsResolved = true;
        }

        private void SpawnBlizzards()
        {
            if (World == null)
                return;

            if (World.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_center.X, _center.Y);
                _center = new Vector3(_center.X, _center.Y, groundZ);
            }

            SoundController.Instance.PlayBuffer(SoundIceStorm1);

            for (int i = 0; i < BlizzardCount; i++)
            {
                float offsetX = MuGame.Random.Next(-180, 180);
                float offsetY = MuGame.Random.Next(-180, 180);
                float spawnZ = _center.Z + 500f + (30f * i);

                Vector3 spawnPos = new Vector3(_center.X + offsetX, _center.Y + offsetY, spawnZ);
                float scale = 0.2f + (MuGame.Random.Next(0, 15) * 0.01f);

                var blizzard = new FallingBlizzardModel(_blizzardPath, this, spawnPos)
                {
                    Position = spawnPos,
                    Angle = Vector3.Zero,
                    Scale = scale
                };

                World.Objects.Add(blizzard);
                _ = blizzard.Load();
            }
        }

        private void OnBlizzardImpact(Vector3 impactPosition)
        {
            if (World == null)
                return;

            bool playSound = _impactCount == 0;
            _impactCount++;

            Vector3 debrisPos = new Vector3(impactPosition.X, impactPosition.Y, impactPosition.Z + ImpactZOffset);

            string stonePath = _iceStonePaths[MuGame.Random.Next(0, _iceStonePaths.Length)];
            var debris = new IceDebrisModel(stonePath)
            {
                Position = debrisPos,
                Angle = new Vector3(
                    MuGame.Random.Next(0, 360),
                    MuGame.Random.Next(0, 360),
                    MuGame.Random.Next(0, 360)),
                Scale = 0.15f + (MuGame.Random.Next(0, 15) * 0.01f)
            };

            World.Objects.Add(debris);
            _ = debris.Load();

            var impact = new IceImpactModel(_iceImpactPath, playSound)
            {
                Position = debrisPos,
                Angle = Vector3.Zero,
                Scale = 0.5f
            };

            World.Objects.Add(impact);
            _ = impact.Load();
        }

        private void UpdateDynamicLight(float dt)
        {
            _time += dt;

            float pulse = 0.8f + 0.2f * MathF.Sin(_time * 8f);
            _impactLight.Position = _center;
            _impactLight.Intensity = _blizzardsSpawned ? 1.1f * pulse : 0f;
            _impactLight.Radius = 275f + 25f * MathF.Sin(_time * 5f);
        }

        private static async Task<string> ResolveIndexedModelPath(string baseName, int index, string fallback)
        {
            if (index > 0)
            {
                string zeroPath = $"Skill/{baseName}0{index}.bmd";
                if (await BMDLoader.Instance.AssestExist(zeroPath))
                    return zeroPath;

                string plainPath = $"Skill/{baseName}{index}.bmd";
                if (await BMDLoader.Instance.AssestExist(plainPath))
                    return plainPath;
            }

            if (await BMDLoader.Instance.AssestExist(fallback))
                return fallback;

            return fallback;
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

        private sealed class FallingBlizzardModel : ModelObject
        {
            private readonly string _path;
            private readonly ScrollOfIceStormEffect _parent;
            private float _lifeFrames;
            private float _gravity;
            private Vector3 _startPosition;
            private Vector3 _lightColor;

            public FallingBlizzardModel(string path, ScrollOfIceStormEffect parent, Vector3 startPos)
            {
                _path = path;
                _parent = parent;
                _startPosition = startPos;
                _lifeFrames = MuGame.Random.Next(15, 31);
                _gravity = -20f - MuGame.Random.Next(0, 21);
                _lightColor = Vector3.Zero;

                ContinuousAnimation = true;
                AnimationSpeed = 3f + (MuGame.Random.Next(0, 10) * 0.1f);
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

                Position = new Vector3(
                    _startPosition.X + MathF.Sin(MuGame.Random.Next(0, 1000) * 0.01f) * 10f,
                    _startPosition.Y + MathF.Sin(MuGame.Random.Next(0, 1000) * 0.01f) * 10f,
                    Position.Z + (_gravity * factor)
                );

                _gravity -= MuGame.Random.Next(0, 6) * factor;
                _startPosition = new Vector3(_startPosition.X - (10f * factor), _startPosition.Y, _startPosition.Z);

                _lightColor.X = Math.Min(1.0f, _lightColor.X + (0.1f * factor));
                _lightColor.Y = _lightColor.X;
                _lightColor.Z = _lightColor.X;

                _lifeFrames -= factor;

                if (World?.Terrain != null)
                {
                    float terrainHeight = World.Terrain.RequestTerrainHeight(Position.X, Position.Y);
                    if (Position.Z < terrainHeight)
                    {
                        _parent.OnBlizzardImpact(new Vector3(Position.X, Position.Y, terrainHeight));
                        RemoveSelf();
                        return;
                    }
                }

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

        private sealed class IceDebrisModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;

            public IceDebrisModel(string path)
            {
                _path = path;
                _lifeFrames = MuGame.Random.Next(20, 31);

                ContinuousAnimation = true;
                AnimationSpeed = 3f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = -2;
                BlendMeshLight = 1.0f;
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
                BlendMeshLight = Math.Max(0f, (_lifeFrames / 30f));
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

        private sealed class IceImpactModel : ModelObject
        {
            private readonly string _path;
            private readonly bool _playSound;
            private float _lifeFrames;
            private bool _soundPlayed;

            public IceImpactModel(string path, bool playSound)
            {
                _path = path;
                _playSound = playSound;
                _lifeFrames = 20f;

                ContinuousAnimation = true;
                AnimationSpeed = 3f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = -2;
                BlendMeshLight = 1.0f;
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

                if (_playSound && !_soundPlayed && (int)_lifeFrames == 18)
                {
                    SoundController.Instance.PlayBuffer(SoundIceStorm2);
                    _soundPlayed = true;
                }

                BlendMeshLight = Math.Max(0f, _lifeFrames * 0.05f);
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
