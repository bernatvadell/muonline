#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Death Stab (Orb of Blow) visual effect (Skill ID 43) using original MU assets.
    /// Three-phase effect: energy charge-up, spike attack, and victim lightning impact.
    /// </summary>
    public sealed class DeathStabEffect : WorldObject
    {
        private const string EnergyParticleBaseName = "RidingSpear";
        private const string SpikeBaseName = "Spear";
        private const string SoundAttack = "Sound/eRageBlow_2.wav";

        private const float EnergySpawnDistance = 600f;
        private const float EnergyTargetDistance = 300f;
        private const float EnergySpawnRandomRadius = 300f;
        private const float EnergyParticleLifeFrames = 20f;

        private const float SpikeBaseDistance = 100f;
        private const float SpikeLifeFrames = 10f;
        private const float TotalLifeFrames = 20f;

        private readonly WalkerObject _caster;
        private readonly WalkerObject? _target;
        private float _lifeTimeFrames = TotalLifeFrames;
        private Vector3 _weaponTipPosition;
        private bool _soundPlayed;
        private bool _impactApplied;

        private string _energyPath = "Skill/RidingSpear01.bmd";
        private string _spikePath = "Item/Spear02.bmd";
        private bool _pathsResolved;

        public DeathStabEffect(WalkerObject caster, WalkerObject? target)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _target = target;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-200f, -200f, -80f),
                new Vector3(200f, 200f, 200f));
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await ResolvePaths();
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

            int lifeInt = (int)_lifeTimeFrames;

            // Phase 1: Energy Charge (frames 12-18, counting down from 20)
            if (lifeInt >= 12 && lifeInt <= 18)
            {
                UpdateWeaponTipPosition();
                SpawnEnergyParticles();
            }

            // Phase 2: Spike Attack (frames 8-14)
            if (lifeInt >= 8 && lifeInt <= 14)
            {
                SpawnSpikeEffects();

                if (lifeInt == 12 && !_soundPlayed)
                {
                    SoundController.Instance.PlayBuffer(SoundAttack);
                    _soundPlayed = true;
                }
            }

            // Phase 3: Target Impact (frame 10)
            if (lifeInt == 10 && !_impactApplied && _target != null)
            {
                ApplyVictimEffect();
                _impactApplied = true;
            }

            _lifeTimeFrames -= FPSCounter.Instance.FPS_ANIMATION_FACTOR;

            if (_lifeTimeFrames <= 0f)
            {
                RemoveSelf();
            }
        }

        private async Task ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _energyPath = await ResolveModelPath(EnergyParticleBaseName, "Skill/", "Skill/RidingSpear01.bmd");
            _spikePath = await ResolveModelPath(SpikeBaseName, "Item/", "Item/Spear02.bmd");

            _pathsResolved = true;
        }

        private static async Task<string> ResolveModelPath(string baseName, string folder, string fallback)
        {
            string path01 = $"{folder}{baseName}01.bmd";
            if (await BMDLoader.Instance.AssestExist(path01))
                return path01;

            string pathPlain = $"{folder}{baseName}.bmd";
            if (await BMDLoader.Instance.AssestExist(pathPlain))
                return pathPlain;

            if (await BMDLoader.Instance.AssestExist(fallback))
                return fallback;

            return path01;
        }

        private void UpdateWeaponTipPosition()
        {
            Vector3 weaponOffset = new Vector3(
                EnergyTargetDistance * MathF.Sin(_caster.Angle.Z),
                -EnergyTargetDistance * MathF.Cos(_caster.Angle.Z),
                120f
            );

            _weaponTipPosition = _caster.Position + weaponOffset;
        }

        private void SpawnEnergyParticles()
        {
            if (World == null || !FPSCounter.Instance.RandFPSCheck(1))
                return;

            for (int i = 0; i < 3; i++)
            {
                Vector3 startPos = _caster.Position;
                startPos.Z += 120f;

                Vector3 randomOffset = new Vector3(
                    MuGame.Random.Next(-(int)EnergySpawnRandomRadius, (int)EnergySpawnRandomRadius),
                    MuGame.Random.Next(-(int)EnergySpawnRandomRadius, (int)EnergySpawnRandomRadius),
                    0f
                );
                startPos += randomOffset;

                Vector3 behindOffset = new Vector3(
                    -EnergySpawnDistance * MathF.Sin(_caster.Angle.Z),
                    EnergySpawnDistance * MathF.Cos(_caster.Angle.Z),
                    0f
                );
                startPos += behindOffset;

                var particle = new DeathStabEnergyParticle(_energyPath, startPos, _weaponTipPosition, EnergyParticleLifeFrames)
                {
                    Angle = _caster.Angle
                };

                World.Objects.Add(particle);
                _ = particle.Load();
            }
        }

        private void SpawnSpikeEffects()
        {
            if (World == null || !FPSCounter.Instance.RandFPSCheck(2))
                return;

            int frameIntoSpike = 14 - (int)_lifeTimeFrames;
            float distance = SpikeBaseDistance + (frameIntoSpike * 10f);

            Vector3 spikePosition = _caster.Position;
            spikePosition.X += distance * MathF.Sin(_caster.Angle.Z);
            spikePosition.Y += -distance * MathF.Cos(_caster.Angle.Z);
            spikePosition.Z += 120f;

            for (int i = 0; i < 2; i++)
            {
                var spike = new DeathStabSpikeEffect(_spikePath, SpikeLifeFrames)
                {
                    Position = spikePosition,
                    Angle = _caster.Angle
                };

                World.Objects.Add(spike);
                _ = spike.Load();
            }
        }

        private void ApplyVictimEffect()
        {
            if (_target == null || World == null)
                return;

            if (_target.Status == GameControlStatus.Disposed)
                return;

            var victimEffect = new DeathStabVictimEffect(_target);
            World.Objects.Add(victimEffect);
            _ = victimEffect.Load();
        }

        private void RemoveSelf()
        {
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }

        private sealed class DeathStabEnergyParticle : ModelObject
        {
            private readonly string _path;
            private readonly Vector3 _startPosition;
            private readonly Vector3 _targetPosition;
            private float _lifeFrames;
            private readonly float _maxLife;

            public DeathStabEnergyParticle(string path, Vector3 start, Vector3 target, float life)
            {
                _path = path;
                _startPosition = start;
                _targetPosition = target;
                _lifeFrames = life;
                _maxLife = life;

                Position = start;
                ContinuousAnimation = true;
                AnimationSpeed = 5f;
                LightEnabled = true;
                IsTransparent = true;
                BlendState = BlendState.Additive;
                DepthState = DepthStencilState.DepthRead;
                BlendMeshState = BlendState.Additive;
                BlendMesh = -2;
                Scale = 4.5f;
                Alpha = 0.1f;
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

                float progress = 1f - (_lifeFrames / _maxLife);
                Position = Vector3.Lerp(_startPosition, _targetPosition, progress);

                BlendMeshLight = (1f - progress) * 2f;

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

        private sealed class DeathStabSpikeEffect : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;

            public DeathStabSpikeEffect(string path, float life)
            {
                _path = path;
                _lifeFrames = life;

                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                BlendState = BlendState.Additive;
                DepthState = DepthStencilState.DepthRead;
                BlendMeshState = BlendState.Additive;
                BlendMesh = 0;
                Scale = 1.2f;
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

                BlendMeshLight = (_lifeFrames / SpikeLifeFrames) * 1.5f;

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
