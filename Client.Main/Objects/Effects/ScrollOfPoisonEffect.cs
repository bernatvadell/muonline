#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Poison visual effect (Skill ID 1), based on Main 5.2 behavior:
    /// poison core model and ten smoke particles on impact.
    /// </summary>
    public sealed class ScrollOfPoisonEffect : EffectObject
    {
        private const string PoisonBaseName = "Poison";
        private const string SmokeTexturePath = "Effect/Spark03.jpg";
        private const float TotalLifeFrames = 66f;
        private const float ModelLifeFrames = 54f;
        private const int SmokeParticleCount = 10;
        private const float SmokeVisualScale = 8f;

        private readonly Vector3 _center;

        private string _poisonPath = "Skill/Poison01.bmd";
        private bool _pathsResolved;
        private bool _initialized;
        private float _lifeFrames = TotalLifeFrames;
        private float _time;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _smokeTexture = null!;

        private readonly SmokeParticle[] _smokeParticles = new SmokeParticle[SmokeParticleCount];
        private readonly DynamicLight _poisonLight;
        private bool _lightAdded;

        private readonly Color _smokeColor = new(0.38f, 0.86f, 0.45f, 0.95f);

        private struct SmokeParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public float Scale;
        }

        public ScrollOfPoisonEffect(Vector3 center)
        {
            _center = center;
            Position = center;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-180f, -180f, -120f),
                new Vector3(180f, 180f, 240f));

            _poisonLight = new DynamicLight
            {
                Owner = this,
                Position = center,
                Color = new Vector3(0.35f, 0.95f, 0.45f),
                Radius = 230f,
                Intensity = 1.15f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await ResolvePaths();

            _ = await TextureLoader.Instance.Prepare(SmokeTexturePath);
            _spriteBatch = GraphicsManager.Instance.Sprite;
            _smokeTexture = TextureLoader.Instance.GetTexture2D(SmokeTexturePath) ?? GraphicsManager.Instance.Pixel;

            if (World?.Terrain != null && !_lightAdded)
            {
                World.Terrain.AddDynamicLight(_poisonLight);
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

            if (!_initialized)
            {
                SpawnPoisonModel();
                InitializeSmokeParticles();
                _initialized = true;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
            _lifeFrames -= factor;
            _time += dt;

            UpdateSmokeParticles(dt);
            UpdateDynamicLight();

            if (_lifeFrames <= 0f)
                RemoveSelf();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _smokeTexture == null || _spriteBatch == null)
                return;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState, SamplerState.LinearClamp, DepthState))
                {
                    DrawSmokeParticles();
                }
            }
            else
            {
                DrawSmokeParticles();
            }
        }

        private async Task ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _poisonPath = await ResolveModelPath(PoisonBaseName, 1, "Skill/Poison01.bmd", "Skill/Poison1.bmd", "Skill/Poison.bmd");
            _pathsResolved = true;
        }

        private void SpawnPoisonModel()
        {
            var core = new PoisonCoreModel(_poisonPath, ModelLifeFrames)
            {
                Position = Vector3.Zero,
                Angle = Vector3.Zero,
                Scale = 0.9f
            };

            Children.Add(core);
            _ = core.Load();
        }

        private void InitializeSmokeParticles()
        {
            for (int i = 0; i < _smokeParticles.Length; i++)
            {
                float life = RandomRange(0.95f, 1.75f);
                _smokeParticles[i] = new SmokeParticle
                {
                    Position = new Vector3(
                        RandomRange(-28f, 28f),
                        RandomRange(-28f, 28f),
                        RandomRange(10f, 80f)),
                    Velocity = new Vector3(
                        RandomRange(-40f, 40f),
                        RandomRange(-40f, 40f),
                        RandomRange(40f, 95f)),
                    Life = life,
                    MaxLife = life,
                    Rotation = RandomRange(0f, MathHelper.TwoPi),
                    RotationSpeed = RandomRange(-1.8f, 1.8f),
                    Scale = RandomRange(0.65f, 1.25f)
                };
            }
        }

        private void UpdateSmokeParticles(float dt)
        {
            for (int i = 0; i < _smokeParticles.Length; i++)
            {
                if (_smokeParticles[i].Life <= 0f)
                    continue;

                var particle = _smokeParticles[i];
                particle.Life -= dt;

                if (particle.Life <= 0f)
                {
                    particle.Life = 0f;
                    _smokeParticles[i] = particle;
                    continue;
                }

                particle.Position += particle.Velocity * dt;
                particle.Velocity = new Vector3(
                    particle.Velocity.X * 0.96f,
                    particle.Velocity.Y * 0.96f,
                    particle.Velocity.Z + (28f * dt));
                particle.Rotation += particle.RotationSpeed * dt;

                _smokeParticles[i] = particle;
            }
        }

        private void DrawSmokeParticles()
        {
            float effectAlpha = MathHelper.Clamp(_lifeFrames / TotalLifeFrames, 0f, 1f);
            if (effectAlpha <= 0f)
                return;

            for (int i = 0; i < _smokeParticles.Length; i++)
            {
                ref readonly var particle = ref _smokeParticles[i];
                if (particle.Life <= 0f)
                    continue;

                float lifeRatio = particle.Life / particle.MaxLife;
                float alpha = lifeRatio * effectAlpha;
                float scale = particle.Scale * (0.75f + 0.65f * (1f - lifeRatio));

                DrawSmokeSprite(particle.Position, particle.Rotation, scale, alpha);
            }
        }

        private void DrawSmokeSprite(Vector3 localPosition, float rotation, float scale, float alpha)
        {
            Vector3 worldPos = _center + localPosition;
            var viewport = GraphicsDevice.Viewport;
            Vector3 projected = viewport.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            float finalScale = scale * baseScale * SmokeVisualScale;
            float depth = MathHelper.Clamp(projected.Z, 0f, 1f);

            _spriteBatch.Draw(
                _smokeTexture,
                new Vector2(projected.X, projected.Y),
                null,
                _smokeColor * alpha,
                rotation,
                new Vector2(_smokeTexture.Width * 0.5f, _smokeTexture.Height * 0.5f),
                finalScale,
                SpriteEffects.None,
                depth);
        }

        private void UpdateDynamicLight()
        {
            float alpha = MathHelper.Clamp(_lifeFrames / TotalLifeFrames, 0f, 1f);
            float pulse = 0.78f + 0.22f * MathF.Sin(_time * 10f);

            _poisonLight.Position = _center;
            _poisonLight.Intensity = 1.15f * alpha * pulse;
            _poisonLight.Radius = 210f + (28f * MathF.Sin(_time * 6f));
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
                World.Terrain.RemoveDynamicLight(_poisonLight);
                _lightAdded = false;
            }

            base.Dispose();
        }

        private sealed class PoisonCoreModel : ModelObject
        {
            private readonly string _path;
            private float _lifeFrames;
            private readonly float _initialLife;

            public PoisonCoreModel(string path, float lifeFrames)
            {
                _path = path;
                _lifeFrames = lifeFrames;
                _initialLife = lifeFrames;

                ContinuousAnimation = true;
                AnimationSpeed = 3.6f;
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
                BlendMeshLight = MathHelper.Clamp(_lifeFrames / _initialLife, 0f, 1f) * 1.1f;

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
