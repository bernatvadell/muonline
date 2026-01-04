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
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Twister visual effect (Skill ID 8) based on original MU client MODEL_STORM behavior.
    /// </summary>
    public sealed class ScrollOfTwisterEffect : EffectObject
    {
        private const ushort TwisterSkillId = 8;
        private const string StormBaseName = "Storm";
        private const string DefaultStormPath = "Skill/Storm01.bmd";
        private const string SmokeTexturePath = "Effect/smoke02.tga";
        private const string ThunderTexturePath = "Effect/JointThunder01.OZJ";
        private const string StoneBaseName = "Stone";
        private const string SoundTwister = "Sound/sTornado.wav";

        private const float LifeFrames = 59f;
        private const float ImpactZOffset = 0f;
        private const float ThunderHeight = 700f;
        private const float ThunderOffset = 200f;
        private const float SmokeSpawnInterval = 0.035f;
        private const float SmokeSpawnRadius = 120f;
        private const float MoveSpeed = 240f;
        private const float StormSpinSpeed = 8f;

        private const float DamageRadius = 150f;
        private const float DamageTickFrames = 15f;
        private const int MaxHitTargets = 32;
        private const double LastCastMatchWindowMs = 1500;

        private const int MaxSmoke = 24;
        private const int MaxBolts = 4;
        private const int BoltSegments = 6;
        private const float BoltLifeFrames = 12f;

        private readonly WalkerObject _caster;
        private readonly Vector3? _targetPosition;
        private Vector3 _center;
        private Vector3 _moveDirection;

        private string _stormPath = DefaultStormPath;
        private readonly string[] _stonePaths = new string[2];
        private bool _pathsResolved;

        private TwisterStormModel? _storm;
        private float _lifeFrames = LifeFrames;
        private float _time;
        private bool _initialized;
        private bool _soundPlayed;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _smokeTexture = null!;
        private Texture2D _thunderTexture = null!;
        private bool _texturesLoaded;

        private readonly SmokeParticle[] _smokes = new SmokeParticle[MaxSmoke];
        private float _smokeTimer;

        private readonly Vector3[,] _boltPoints = new Vector3[MaxBolts, BoltSegments + 1];
        private readonly float[] _boltLife = new float[MaxBolts];
        private readonly float[] _boltMaxLife = new float[MaxBolts];

        private float _damageFrameAccumulator;
        private byte _animationCounter;
        private byte _hitCounter;
        private byte _castTileX;
        private byte _castTileY;

        private readonly DynamicLight _darkLight;
        private bool _lightAdded;

        private struct SmokeParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float Scale;
        }

        public ScrollOfTwisterEffect(WalkerObject caster, Vector3 center, Vector3? targetPosition = null)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _center = center;
            _targetPosition = targetPosition;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-450f, -450f, -120f),
                new Vector3(450f, 450f, ThunderHeight + 200f));

            _darkLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(-0.35f, -0.25f, -0.2f),
                Radius = 320f,
                Intensity = 0.7f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await ResolvePaths();

            _ = await TextureLoader.Instance.Prepare(SmokeTexturePath);
            _ = await TextureLoader.Instance.Prepare(ThunderTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _smokeTexture = TextureLoader.Instance.GetTexture2D(SmokeTexturePath) ?? GraphicsManager.Instance.Pixel;
            _thunderTexture = TextureLoader.Instance.GetTexture2D(ThunderTexturePath) ?? GraphicsManager.Instance.Pixel;
            _texturesLoaded = true;

            if (World?.Terrain != null && !_lightAdded)
            {
                World.Terrain.AddDynamicLight(_darkLight);
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

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            if (!_initialized)
                InitializeEffect();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            _time += dt;

            _center += _moveDirection * (MoveSpeed * dt);
            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_center.X, _center.Y);
                _center = new Vector3(_center.X, _center.Y, groundZ + ImpactZOffset);
            }
            Position = _center;

            UpdateSmoke(dt);
            UpdateBolts(factor);
            UpdateDynamicLight();
            UpdateDamage(factor);

            TrySpawnSmoke(dt);
            TrySpawnThunder();
            TrySpawnStone();

            _lifeFrames -= factor;
            if (_lifeFrames <= 0f)
            {
                RemoveSelf();
            }

            if (_storm != null)
            {
                _storm.Position = _center;
                _storm.Angle = new Vector3(_storm.Angle.X, _storm.Angle.Y, _storm.Angle.Z + StormSpinSpeed * dt);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!_texturesLoaded || _spriteBatch == null || !Visible)
                return;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthState))
                {
                    DrawSmoke();
                }
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthState))
                {
                    DrawBolts();
                }
            }
            else
            {
                DrawSmoke();
                DrawBolts();
            }
        }

        private void InitializeEffect()
        {
            if (World?.Terrain != null)
            {
                float groundZ = World.Terrain.RequestTerrainHeight(_center.X, _center.Y);
                _center = new Vector3(_center.X, _center.Y, groundZ + ImpactZOffset);
            }

            Position = _center;
            _moveDirection = ResolveMoveDirection();
            _castTileX = (byte)Math.Clamp((int)(_center.X / Constants.TERRAIN_SCALE), 0, Constants.TERRAIN_SIZE - 1);
            _castTileY = (byte)Math.Clamp((int)(_center.Y / Constants.TERRAIN_SCALE), 0, Constants.TERRAIN_SIZE - 1);

            if (!_soundPlayed)
            {
                SoundController.Instance.PlayBuffer(SoundTwister);
                _soundPlayed = true;
            }

            _storm = new TwisterStormModel(_stormPath, LifeFrames)
            {
                Position = _center,
                Angle = Vector3.Zero,
                Scale = 1f
            };

            World?.Objects.Add(_storm);
            _ = _storm.Load();

            _initialized = true;
        }

        private void UpdateDynamicLight()
        {
            if (World?.Terrain == null)
                return;

            float pulse = 0.8f + 0.2f * MathF.Sin(_time * 10f);
            _darkLight.Position = _center;
            _darkLight.Intensity = 0.7f * pulse;
        }

        private void UpdateDamage(float frameFactor)
        {
            if (World is not WalkableWorldControl { Walker: PlayerObject hero })
                return;

            if (_caster is not PlayerObject casterPlayer || casterPlayer != hero)
                return;

            if (hero.IsDead)
                return;

            if (MuGame.Network == null || !MuGame.Network.IsConnected)
                return;

            EnsureAnimationCounterInitialized();

            _damageFrameAccumulator += frameFactor;
            if (_damageFrameAccumulator < DamageTickFrames)
                return;

            _damageFrameAccumulator -= DamageTickFrames;

            Span<ushort> targetBuffer = stackalloc ushort[MaxHitTargets];
            int targetCount = CollectTargetsToHit(targetBuffer);
            if (targetCount <= 0)
                return;

            var targets = new ushort[targetCount];
            for (int i = 0; i < targetCount; i++)
                targets[i] = targetBuffer[i];

            unchecked { _hitCounter++; }

            byte currentTileX = (byte)Math.Clamp((int)(_center.X / Constants.TERRAIN_SCALE), 0, Constants.TERRAIN_SIZE - 1);
            byte currentTileY = (byte)Math.Clamp((int)(_center.Y / Constants.TERRAIN_SCALE), 0, Constants.TERRAIN_SIZE - 1);

            _ = MuGame.Network
                .GetCharacterService()
                .SendAreaSkillHitAsync(TwisterSkillId, currentTileX, currentTileY, _hitCounter, targets, _animationCounter);
        }

        private void EnsureAnimationCounterInitialized()
        {
            if (_animationCounter != 0)
                return;

            var characterState = MuGame.Network?.GetCharacterState();
            if (characterState == null)
                return;

            if (characterState.LastAreaSkillId != TwisterSkillId)
                return;

            double nowMs = MuGame.Instance?.GameTime?.TotalGameTime.TotalMilliseconds ?? Environment.TickCount64;
            double elapsedMs = nowMs - characterState.LastAreaSkillSentAtMs;
            if (elapsedMs < 0 || elapsedMs > LastCastMatchWindowMs)
                return;

            if (characterState.LastAreaSkillTargetX != _castTileX || characterState.LastAreaSkillTargetY != _castTileY)
                return;

            _animationCounter = characterState.LastAreaSkillAnimationCounter;
        }

        private int CollectTargetsToHit(Span<ushort> targetBuffer)
        {
            if (World == null)
                return 0;

            float rangeSq = DamageRadius * DamageRadius;
            int count = 0;

            var monsters = World.Monsters;
            for (int i = 0; i < monsters.Count && count < targetBuffer.Length; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.IsDead)
                    continue;

                float dx = monster.Position.X - _center.X;
                float dy = monster.Position.Y - _center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                    continue;

                targetBuffer[count++] = monster.NetworkId;
            }

            return count;
        }

        private void TrySpawnSmoke(float dt)
        {
            _smokeTimer += dt;
            while (_smokeTimer >= SmokeSpawnInterval)
            {
                _smokeTimer -= SmokeSpawnInterval;
                SpawnSmokeParticle();
            }
        }

        private void SpawnSmokeParticle()
        {
            int index = FindFreeSmoke();
            if (index < 0)
                return;

            float maxLife = RandomRange(0.6f, 1.1f);
            float angle = RandomRange(0f, MathHelper.TwoPi);
            float radius = RandomRange(0f, SmokeSpawnRadius);
            Vector3 pos = _center + new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 40f + RandomRange(0f, 30f));

            Vector3 vel = new Vector3(
                MathF.Cos(angle) * RandomRange(5f, 25f),
                MathF.Sin(angle) * RandomRange(5f, 25f),
                RandomRange(20f, 60f));

            _smokes[index] = new SmokeParticle
            {
                Position = pos,
                Velocity = vel,
                MaxLife = maxLife,
                Life = maxLife,
                Rotation = RandomRange(0f, MathHelper.TwoPi),
                Scale = RandomRange(0.7f, 1.4f)
            };
        }

        private void UpdateSmoke(float dt)
        {
            for (int i = 0; i < MaxSmoke; i++)
            {
                if (_smokes[i].Life <= 0f)
                    continue;

                _smokes[i].Life -= dt;
                if (_smokes[i].Life <= 0f)
                    continue;

                _smokes[i].Position += _smokes[i].Velocity * dt;
                _smokes[i].Velocity *= 1f - MathHelper.Clamp(dt * 0.6f, 0f, 0.2f);
                _smokes[i].Velocity.Z += 10f * dt;
                _smokes[i].Rotation += dt * 0.6f;
            }
        }

        private void DrawSmoke()
        {
            Color baseColor = new Color(0.55f, 0.65f, 0.85f, 0.8f);

            for (int i = 0; i < MaxSmoke; i++)
            {
                if (_smokes[i].Life <= 0f)
                    continue;

                float t = _smokes[i].Life / _smokes[i].MaxLife;
                float alpha = MathHelper.Clamp(t, 0f, 1f);
                float scale = MathHelper.Lerp(1.4f, 2.2f, 1f - t) * _smokes[i].Scale;

                DrawSprite(_smokeTexture, _smokes[i].Position, baseColor * (alpha * 0.7f), _smokes[i].Rotation, new Vector2(scale, scale));
            }
        }

        private void TrySpawnThunder()
        {
            if (FPSCounter.Instance.RandFPSCheck(2))
                SpawnThunderBolt(-ThunderOffset);

            if (FPSCounter.Instance.RandFPSCheck(2))
                SpawnThunderBolt(ThunderOffset);
        }

        private void SpawnThunderBolt(float offsetX)
        {
            int index = FindFreeBolt();
            if (index < 0)
                return;

            float offsetY = MuGame.Random.Next(-60, 60);

            Vector3 start = _center + new Vector3(offsetX, offsetY, ThunderHeight);
            Vector3 end = _center + new Vector3(RandomRange(-30f, 30f), RandomRange(-30f, 30f), 10f);

            BuildBoltPoints(index, start, end);
            _boltLife[index] = BoltLifeFrames;
            _boltMaxLife[index] = BoltLifeFrames;
        }

        private void BuildBoltPoints(int index, Vector3 start, Vector3 end)
        {
            Vector3 dir = end - start;
            float length = dir.Length();
            if (length < 0.1f)
            {
                for (int i = 0; i <= BoltSegments; i++)
                    _boltPoints[index, i] = start;
                return;
            }

            dir /= length;
            Vector3 perp = new Vector3(-dir.Y, dir.X, 0f);
            if (perp.LengthSquared() < 0.001f)
                perp = Vector3.UnitX;

            for (int i = 0; i <= BoltSegments; i++)
            {
                float t = i / (float)BoltSegments;
                Vector3 point = Vector3.Lerp(start, end, t);
                float fade = 1f - MathF.Abs(t - 0.5f) * 2f;
                float offset = RandomRange(-60f, 60f) * fade;
                point += perp * offset;
                point.Z += RandomRange(-40f, 40f) * fade;
                _boltPoints[index, i] = point;
            }
        }

        private void UpdateBolts(float factor)
        {
            for (int i = 0; i < MaxBolts; i++)
            {
                if (_boltLife[i] <= 0f)
                    continue;

                _boltLife[i] -= factor;
                if (_boltLife[i] <= 0f)
                {
                    _boltLife[i] = 0f;
                }
            }
        }

        private void DrawBolts()
        {
            Texture2D tex = _thunderTexture ?? GraphicsManager.Instance.Pixel;
            if (tex == null)
                return;

            float invTexWidth = tex.Width > 0 ? 1f / tex.Width : 1f;
            Vector2 origin = new Vector2(0f, tex.Height * 0.5f);

            for (int b = 0; b < MaxBolts; b++)
            {
                if (_boltLife[b] <= 0f)
                    continue;

                float alpha = MathHelper.Clamp(_boltLife[b] / _boltMaxLife[b], 0f, 1f);
                float thickness = 0.9f + 0.5f * alpha;
                Color color = new Color(0.5f, 0.7f, 1f) * alpha;

                for (int i = 0; i < BoltSegments; i++)
                {
                    Vector3 p0 = Project(_boltPoints[b, i]);
                    Vector3 p1 = Project(_boltPoints[b, i + 1]);

                    if (p0.Z < 0f || p0.Z > 1f || p1.Z < 0f || p1.Z > 1f)
                        continue;

                    Vector2 s0 = new Vector2(p0.X, p0.Y);
                    Vector2 s1 = new Vector2(p1.X, p1.Y);
                    Vector2 delta = s1 - s0;
                    float length = delta.Length();
                    if (length < 1f)
                        continue;

                    float rotation = MathF.Atan2(delta.Y, delta.X);
                    float depth = MathHelper.Clamp((p0.Z + p1.Z) * 0.5f, 0f, 1f);
                    Vector2 scale = new Vector2(length * invTexWidth * 1.05f, thickness);

                    _spriteBatch.Draw(
                        tex,
                        s0,
                        null,
                        color,
                        rotation,
                        origin,
                        scale,
                        SpriteEffects.None,
                        depth);
                }
            }
        }

        private Vector3 Project(Vector3 worldPos)
        {
            return GraphicsDevice.Viewport.Project(
                worldPos,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);
        }

        private void TrySpawnStone()
        {
            if (World == null || !FPSCounter.Instance.RandFPSCheck(4))
                return;

            string stonePath = _stonePaths[MuGame.Random.Next(0, _stonePaths.Length)];
            Vector3 offset = new Vector3(
                RandomRange(-110f, 110f),
                RandomRange(-110f, 110f),
                10f);

            Vector3 velocity = new Vector3(
                RandomRange(-45f, 45f),
                RandomRange(-45f, 45f),
                RandomRange(50f, 120f));

            var stone = new TwisterStoneModel(stonePath, 24f, velocity)
            {
                Position = _center + offset,
                Angle = Vector3.Zero,
                Scale = 0.9f
            };

            World.Objects.Add(stone);
            _ = stone.Load();
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

        private async Task ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _stormPath = await ResolveIndexedModelPath(StormBaseName, 1, DefaultStormPath);
            _stonePaths[0] = await ResolveIndexedModelPath(StoneBaseName, 1, "Skill/Stone.bmd");
            _stonePaths[1] = await ResolveIndexedModelPath(StoneBaseName, 2, _stonePaths[0]);

            _pathsResolved = true;
        }

        private int FindFreeSmoke()
        {
            for (int i = 0; i < MaxSmoke; i++)
            {
                if (_smokes[i].Life <= 0f)
                    return i;
            }
            return -1;
        }

        private int FindFreeBolt()
        {
            for (int i = 0; i < MaxBolts; i++)
            {
                if (_boltLife[i] <= 0f)
                    return i;
            }
            return -1;
        }

        private static float RandomRange(float min, float max)
        {
            return (float)(MuGame.Random.NextDouble() * (max - min) + min);
        }

        private Vector3 ResolveMoveDirection()
        {
            if (_targetPosition.HasValue)
            {
                Vector3 delta = _targetPosition.Value - _center;
                delta.Z = 0f;
                if (delta.LengthSquared() > 0.001f)
                    return Vector3.Normalize(delta);
            }

            return new Vector3(MathF.Sin(_caster.Angle.Z), -MathF.Cos(_caster.Angle.Z), 0f);
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, Vector2 scale)
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
            if (_lightAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_darkLight);
                _lightAdded = false;
            }

            base.Dispose();
        }

        private sealed class TwisterStormModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;

            public TwisterStormModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 7f;
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

        private sealed class TwisterStoneModel : ModelObject
        {
            private readonly string _path;
            private Vector3 _velocity;
            private float _lifeFrames;

            public TwisterStoneModel(string path, float lifeFrames, Vector3 initialVelocity)
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
                Position += _velocity * (0.16f * factor);
                _velocity = new Vector3(_velocity.X * 0.98f, _velocity.Y * 0.98f, _velocity.Z - 2.4f * factor);
                Angle = new Vector3(Angle.X + 0.3f * factor, Angle.Y + 0.4f * factor, Angle.Z + 0.5f * factor);
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
