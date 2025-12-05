using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class BonfireObject : ModelObject
    {
        private static readonly Random _random = new Random();

        private List<FireHik01Effect> _topFlames;
        private List<FireHik02Effect> _middleFlames;
        private List<FireHik03Effect> _baseFlames;

        // The dynamic light source for this bonfire.
        private DynamicLight _dynamicLight;

        // A unique time offset for this bonfire instance.
        private float _timeOffset;

        private float _baseHeight = 30f;
        private Vector3 _basePosition;
        private List<float> _individualWindTimes;
        private List<float> _individualWindStrengths;
        private List<float> _scaleOffsets;

        private const float MIN_ALPHA = 0.6f;
        private const float MAX_ALPHA = 0.8f;
        private const float WIND_CHANGE_SPEED = 0.4f;
        private const float MAX_WIND_STRENGTH = 0.5f;
        private const int FLAME_COUNT = 3;

        private const float SCALE_CHANGE_SPEED = 0.9f;
        private const float RANDOM_SCALE_INFLUENCE = 0.15f;

        private readonly Vector3 BaseFlameColor = new Vector3(1.0f, 0.45f, 0.15f);
        private readonly Vector3 MiddleFlameColor = new Vector3(1.0f, 0.65f, 0.25f);
        private readonly Vector3 TopFlameColor = new Vector3(1.0f, 0.75f, 0.35f);

        // Sparks system - procedural rendering
        private const int SPARK_COUNT = 8;
        private Vector3[] _sparkPositions;
        private Vector3[] _sparkVelocities;
        private float[] _sparkLifetimes;
        private float[] _sparkMaxLifetimes;

        // Smoke particle system - procedural texture, no external assets
        private const int SMOKE_PARTICLE_COUNT = 10;
        private Vector3[] _smokePositions;
        private Vector3[] _smokeVelocities;
        private float[] _smokeLives;
        private float[] _smokeMaxLives;
        private float[] _smokeScales;
        private float[] _smokeRotations;

        // Shared procedural textures
        private static Texture2D _sparkTexture;
        private static Texture2D _smokeTexture;
        private static Vector2 _textureCenter;

        public BonfireObject()
        {
            LightEnabled = true;
            BlendMesh = 1;

            _timeOffset = (float)_random.NextDouble() * 1000f;

            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.7f, 0.4f),
                Radius = 450f, // Bonfires are usually larger
                Intensity = 1.2f
            };

            _topFlames = new List<FireHik01Effect>();
            _middleFlames = new List<FireHik02Effect>();
            _baseFlames = new List<FireHik03Effect>();
            _individualWindTimes = new List<float>();
            _individualWindStrengths = new List<float>();
            _scaleOffsets = new List<float>();

            for (int i = 0; i < FLAME_COUNT; i++)
            {
                var topFlame = new FireHik01Effect();
                var middleFlame = new FireHik02Effect();
                var baseFlame = new FireHik03Effect();

                Children.Add(topFlame);
                Children.Add(middleFlame);
                Children.Add(baseFlame);

                _topFlames.Add(topFlame);
                _middleFlames.Add(middleFlame);
                _baseFlames.Add(baseFlame);

                _individualWindTimes.Add((float)_random.NextDouble() * MathHelper.TwoPi);
                _individualWindStrengths.Add((float)_random.NextDouble() * MAX_WIND_STRENGTH);
                _scaleOffsets.Add((float)_random.NextDouble() * MathHelper.TwoPi);
            }

            // Initialize sparks system
            _sparkPositions = new Vector3[SPARK_COUNT];
            _sparkVelocities = new Vector3[SPARK_COUNT];
            _sparkLifetimes = new float[SPARK_COUNT];
            _sparkMaxLifetimes = new float[SPARK_COUNT];

            for (int i = 0; i < SPARK_COUNT; i++)
            {
                _sparkLifetimes[i] = -(float)_random.NextDouble() * 0.8f;
            }

            // Initialize smoke particle arrays
            _smokePositions = new Vector3[SMOKE_PARTICLE_COUNT];
            _smokeVelocities = new Vector3[SMOKE_PARTICLE_COUNT];
            _smokeLives = new float[SMOKE_PARTICLE_COUNT];
            _smokeMaxLives = new float[SMOKE_PARTICLE_COUNT];
            _smokeScales = new float[SMOKE_PARTICLE_COUNT];
            _smokeRotations = new float[SMOKE_PARTICLE_COUNT];

            for (int i = 0; i < SMOKE_PARTICLE_COUNT; i++)
            {
                _smokeLives[i] = -(float)_random.NextDouble() * 2f;
            }
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Object1/Bonfire01.bmd");
            await base.Load();

            // Generate textures once (shared between all bonfires)
            if (_sparkTexture == null || _smokeTexture == null)
            {
                GenerateProceduralTextures();
            }

            // Add the dynamic light to the world when the object is loaded.
            if (World != null)
            {
                World.Terrain.AddDynamicLight(_dynamicLight);
            }
        }

        private static void GenerateProceduralTextures()
        {
            const int size = 32;
            var device = GraphicsManager.Instance.GraphicsDevice;
            float center = size * 0.5f;
            float maxRadius = center;

            // Generate spark texture - bright center, sharp falloff
            _sparkTexture = new Texture2D(device, size, size);
            var sparkPixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    float t = MathHelper.Clamp(1f - dist / maxRadius, 0f, 1f);
                    // Sharp bright center
                    float intensity = t * t * t;

                    sparkPixels[y * size + x] = new Color(intensity, intensity, intensity, intensity);
                }
            }
            _sparkTexture.SetData(sparkPixels);

            // Generate smoke texture - soft edges
            _smokeTexture = new Texture2D(device, size, size);
            var smokePixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    float t = MathHelper.Clamp(1f - dist / maxRadius, 0f, 1f);
                    float alpha = t * t * (3f - 2f * t);
                    alpha *= alpha;

                    smokePixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
                }
            }
            _smokeTexture.SetData(smokePixels);

            _textureCenter = new Vector2(size * 0.5f, size * 0.5f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Use the offset time for all calculations to desynchronize.
            var time = (float)gameTime.TotalGameTime.TotalSeconds + _timeOffset;
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            UpdateIndividualWindEffects(time);
            var baseLuminosity = CalculateBaseLuminosity(time);
            _basePosition = BoneTransform[3].Translation;

            // Update both the visual effects and the dynamic light.
            UpdateDynamicLight(baseLuminosity);
            UpdateFireEffects(time, baseLuminosity);
            UpdateSparks(deltaTime, time);
            UpdateSmoke(deltaTime, time);
        }

        private void UpdateDynamicLight(float intensity)
        {
            _dynamicLight.Position = GetFlameLightPosition();
            _dynamicLight.Intensity = intensity;
        }

        private Vector3 GetFlameLightPosition()
        {
            Vector3 sum = Vector3.Zero;
            int count = 0;

            AccumulateFlamePositions(_baseFlames, ref sum, ref count);
            AccumulateFlamePositions(_middleFlames, ref sum, ref count);
            AccumulateFlamePositions(_topFlames, ref sum, ref count);

            if (count > 0)
                return sum / count;

            Vector3 fallbackLocal = _basePosition + new Vector3(0f, 0f, _baseHeight);
            return Vector3.Transform(fallbackLocal, WorldPosition);
        }

        private static void AccumulateFlamePositions<T>(IReadOnlyList<T> flames, ref Vector3 sum, ref int count)
            where T : SpriteObject
        {
            for (int i = 0; i < flames.Count; i++)
            {
                var flame = flames[i];
                if (flame?.Status == GameControlStatus.Ready)
                {
                    sum += flame.WorldPosition.Translation;
                    count++;
                }
            }
        }

        private void UpdateIndividualWindEffects(float time)
        {
            for (int i = 0; i < FLAME_COUNT; i++)
            {
                _individualWindTimes[i] += WIND_CHANGE_SPEED * 0.016f * (1 + (float)_random.NextDouble() * 0.2f);
                _individualWindStrengths[i] = CalculateIndividualWindStrength(i, time);
            }
        }

        private float CalculateIndividualWindStrength(int index, float time)
        {
            float baseWind = (float)Math.Sin(_individualWindTimes[index]);
            float randomOffset = (float)Math.Sin(time * (1.5f + index * 0.2f));
            return (baseWind * 0.7f + randomOffset * 0.3f) * MAX_WIND_STRENGTH;
        }

        private float CalculateBaseLuminosity(float time)
        {
            return 0.9f +
                   (float)Math.Sin(time * 1.8f) * 0.15f +
                   (float)Math.Sin(time * 3.7f) * 0.08f;
        }

        private Vector3 CalculateIndividualFlameOffset(int index, float time, float baseFrequency, float amplitude)
        {
            float individualPhase = _individualWindTimes[index];
            var windOffset = _individualWindStrengths[index] * new Vector3(1f, 0.4f, 0f);

            return new Vector3(
                (float)Math.Sin(time * baseFrequency + individualPhase) * amplitude * (0.8f + (float)_random.NextDouble() * 0.4f),
                (float)Math.Cos(time * baseFrequency * 0.9f + individualPhase) * amplitude * (0.7f + (float)_random.NextDouble() * 0.3f),
                (float)Math.Sin(time * (baseFrequency * 1.1f) + individualPhase) * (amplitude * 0.4f)
            ) + windOffset;
        }

        private void UpdateFireEffects(float time, float baseLuminosity)
        {
            var turbulence = CalculateTurbulence(time);
            for (int i = 0; i < FLAME_COUNT; i++)
            {
                _scaleOffsets[i] += SCALE_CHANGE_SPEED * 0.016f;
                UpdateBaseFlame(i, time, baseLuminosity, turbulence);
                UpdateMiddleFlame(i, time, baseLuminosity, turbulence);
                UpdateTopFlame(i, time, baseLuminosity, turbulence);
            }
        }

        private float CalculateTurbulence(float time)
        {
            return 1.0f + (float)(
                Math.Sin(time * 4.0f) * 0.12f +
                Math.Sin(time * 9.0f) * 0.06f +
                Math.Sin(time * 15.0f) * 0.03f
            );
        }

        private float CalculateFlameScale(int index, float baseScale, float turbulence, float time)
        {
            float smoothRandomFactor = (float)Math.Sin(_scaleOffsets[index]) * RANDOM_SCALE_INFLUENCE;
            return baseScale + (turbulence - 1.0f) * 0.2f + smoothRandomFactor;
        }

        private void UpdateBaseFlame(int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = _baseFlames[index];
            float individualIntensity = 0.8f + (float)Math.Sin(_scaleOffsets[index]) * 0.1f;
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(index, time, 1.8f, 7.0f);
            flame.Position = _basePosition + offset + new Vector3(0f, 0f, _baseHeight);

            var colorIntensity = 0.85f + turbulence * 0.15f;
            flame.Light = BaseFlameColor * flameIntensity * colorIntensity;
            flame.Scale = CalculateFlameScale(index, 2.3f, turbulence, time);
        }

        private void UpdateMiddleFlame(int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = _middleFlames[index];
            float individualIntensity = 0.75f + (float)Math.Sin(_scaleOffsets[index]) * 0.15f;
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(index, time, 2.2f, 6.0f);
            flame.Position = _basePosition + offset + new Vector3(0f, 0f, _baseHeight + 18f);

            var colorIntensity = 0.9f + turbulence * 0.1f;
            flame.Light = MiddleFlameColor * flameIntensity * colorIntensity;
            flame.Scale = CalculateFlameScale(index, 2.7f, turbulence, time);
        }

        private void UpdateTopFlame(int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = _topFlames[index];
            float individualIntensity = 0.7f + (float)Math.Sin(_scaleOffsets[index]) * 0.2f;
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(index, time, 2.5f, 5.0f);
            var heightVariation = (float)Math.Sin(time * 2.0f + _individualWindTimes[index]) * 12.0f +
                                _individualWindStrengths[index] * 10.0f;

            flame.Position = _basePosition + offset + new Vector3(
                _individualWindStrengths[index] * 4.0f,
                0f,
                _baseHeight + 35f + heightVariation
            );

            var colorIntensity = 0.95f + turbulence * 0.1f;
            flame.Light = TopFlameColor * flameIntensity * colorIntensity;
            flame.Scale = CalculateFlameScale(index, 2.9f, turbulence, time);
        }

        private float ClampAlpha(float alpha)
        {
            return Math.Max(MIN_ALPHA, Math.Min(MAX_ALPHA, alpha));
        }

        private void UpdateSparks(float deltaTime, float time)
        {
            for (int i = 0; i < SPARK_COUNT; i++)
            {
                if (_sparkLifetimes[i] <= 0f)
                {
                    RespawnSpark(i);
                }
                else
                {
                    _sparkLifetimes[i] -= deltaTime;

                    // Move spark upward with velocity
                    _sparkPositions[i] += _sparkVelocities[i] * deltaTime;

                    // Add slight horizontal drift (wind effect)
                    _sparkVelocities[i].X += (float)(_random.NextDouble() - 0.5) * 40f * deltaTime;
                    _sparkVelocities[i].Y += (float)(_random.NextDouble() - 0.5) * 40f * deltaTime;
                    // Gravity and air resistance
                    _sparkVelocities[i].Z *= 0.98f;
                }
            }
        }

        private void RespawnSpark(int index)
        {
            // Random spawn position near flames
            float offsetX = (float)(_random.NextDouble() - 0.5) * 18f;
            float offsetY = (float)(_random.NextDouble() - 0.5) * 18f;
            _sparkPositions[index] = _basePosition + new Vector3(offsetX, offsetY, _baseHeight + 20f);

            // Upward velocity with random horizontal component
            _sparkVelocities[index] = new Vector3(
                (float)(_random.NextDouble() - 0.5) * 60f,
                (float)(_random.NextDouble() - 0.5) * 60f,
                70f + (float)_random.NextDouble() * 60f
            );

            // Random lifetime
            _sparkMaxLifetimes[index] = 1.2f + (float)_random.NextDouble() * 1.8f;
            _sparkLifetimes[index] = _sparkMaxLifetimes[index];
        }

        private void UpdateSmoke(float deltaTime, float time)
        {
            for (int i = 0; i < SMOKE_PARTICLE_COUNT; i++)
            {
                _smokeLives[i] -= deltaTime;

                // Allow particle to exist for extra fade-out grace period (1 second after "death")
                // Only respawn when well past the grace period
                if (_smokeLives[i] < -1.0f)
                {
                    RespawnSmoke(i);
                }

                // Continue updating position and scale even during fade-out grace period
                // Move smoke upward
                _smokePositions[i] += _smokeVelocities[i] * deltaTime;

                // Add wind drift
                float windPhase = time * 0.4f + i * 0.7f;
                _smokeVelocities[i].X = (float)Math.Sin(windPhase) * 10f;
                _smokeVelocities[i].Y = (float)Math.Cos(windPhase * 0.6f) * 6f;

                // Slow rotation
                _smokeRotations[i] += deltaTime * 0.2f;

                // Grow over time - use clamped ratio to avoid negative scale
                float lifeRatio = Math.Max(0f, _smokeLives[i] / _smokeMaxLives[i]);
                _smokeScales[i] = 1.0f + (1f - lifeRatio) * 3.0f;
            }
        }

        private void RespawnSmoke(int index)
        {
            // Spawn above flames
            float offsetX = (float)(_random.NextDouble() - 0.5) * 15f;
            float offsetY = (float)(_random.NextDouble() - 0.5) * 15f;
            _smokePositions[index] = _basePosition + new Vector3(offsetX, offsetY, _baseHeight + 55f);

            // Slow upward velocity
            _smokeVelocities[index] = new Vector3(
                (float)(_random.NextDouble() - 0.5) * 6f,
                (float)(_random.NextDouble() - 0.5) * 6f,
                18f + (float)_random.NextDouble() * 12f
            );

            // Lifetime 4-7 seconds
            _smokeMaxLives[index] = 4f + (float)_random.NextDouble() * 3f;
            _smokeLives[index] = _smokeMaxLives[index];
            _smokeScales[index] = 1.0f;
            _smokeRotations[index] = (float)_random.NextDouble() * MathHelper.TwoPi;
        }

        public override void DrawAfter(GameTime gameTime)
        {
            base.DrawAfter(gameTime);
            DrawParticles();
        }

        private void DrawParticles()
        {
            if (_sparkTexture == null || _smokeTexture == null || Status != GameControlStatus.Ready)
                return;

            var camera = Camera.Instance;
            if (camera == null) return;

            var device = GraphicsManager.Instance.GraphicsDevice;
            var spriteBatch = GraphicsManager.Instance.Sprite;
            var viewport = device.Viewport;
            var viewProj = camera.View * camera.Projection;

            // Save states
            var prevBlend = device.BlendState;
            var prevDepth = device.DepthStencilState;

            // Draw everything with Additive blending
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.DepthRead,
                RasterizerState.CullNone
            );

            // Draw smoke as light mist/steam (additive = bright)
            for (int i = 0; i < SMOKE_PARTICLE_COUNT; i++)
            {
                // Skip only when particle is in respawn wait state
                if (_smokeLives[i] < -1.0f) continue;

                Vector3 worldPos = Vector3.Transform(_smokePositions[i], WorldPosition);
                Vector4 clipPos = Vector4.Transform(worldPos, viewProj);
                if (clipPos.W <= 0.001f) continue;

                float invW = 1f / clipPos.W;
                float screenX = (clipPos.X * invW * 0.5f + 0.5f) * viewport.Width;
                float screenY = (0.5f - clipPos.Y * invW * 0.5f) * viewport.Height;

                if (screenX < -300 || screenX > viewport.Width + 300 ||
                    screenY < -300 || screenY > viewport.Height + 300) continue;

                // Calculate alpha with extended fade-out grace period
                float lifeRatio = _smokeLives[i] / _smokeMaxLives[i];
                float alpha;

                if (lifeRatio > 0.8f)
                {
                    // Fade in (first 20% of life)
                    alpha = (1f - lifeRatio) / 0.2f;
                }
                else if (lifeRatio > 0.3f)
                {
                    // Full opacity (20% to 70% of life)
                    alpha = 1f;
                }
                else if (lifeRatio > 0f)
                {
                    // Normal fade-out (70% to 100% of life)
                    alpha = lifeRatio / 0.3f;
                }
                else
                {
                    // Extended fade-out grace period (lifeRatio <= 0, i.e. _smokeLives negative)
                    // Fade from ~0 to fully invisible over 1 second grace period
                    // _smokeLives goes from 0 to -1, so we map it to alpha going from small to 0
                    float graceFactor = 1f + _smokeLives[i]; // Goes from 1 to 0 as _smokeLives goes from 0 to -1
                    alpha = graceFactor * 0.3f; // Start from 30% (where normal fade ended) and go to 0
                }

                alpha *= 0.2f; // Slightly more visible smoke

                // Skip if effectively invisible
                if (alpha < 0.001f) continue;

                // Light gray/white smoke color
                byte c = (byte)(alpha * 200);
                Color smokeColor = new Color(c, c, c);

                float distScale = MathHelper.Clamp(600f / clipPos.W, 0.4f, 2.5f);
                float finalScale = _smokeScales[i] * distScale * Constants.RENDER_SCALE;

                spriteBatch.Draw(
                    _smokeTexture,
                    new Vector2(screenX, screenY),
                    null,
                    smokeColor,
                    _smokeRotations[i],
                    _textureCenter,
                    finalScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // Draw sparks
            float time = (float)_timeOffset;

            for (int i = 0; i < SPARK_COUNT; i++)
            {
                if (_sparkLifetimes[i] <= 0f) continue;

                Vector3 worldPos = Vector3.Transform(_sparkPositions[i], WorldPosition);
                Vector4 clipPos = Vector4.Transform(worldPos, viewProj);
                if (clipPos.W <= 0.001f) continue;

                float invW = 1f / clipPos.W;
                float screenX = (clipPos.X * invW * 0.5f + 0.5f) * viewport.Width;
                float screenY = (0.5f - clipPos.Y * invW * 0.5f) * viewport.Height;

                if (screenX < -100 || screenX > viewport.Width + 100 ||
                    screenY < -100 || screenY > viewport.Height + 100) continue;

                float lifeRatio = _sparkLifetimes[i] / _sparkMaxLifetimes[i];
                float alpha = lifeRatio;

                // Orange-yellow spark color with flicker
                float flicker = 0.8f + (float)Math.Sin(time * 30f + i * 5f) * 0.2f;
                Color sparkColor = new Color(
                    (byte)(255 * alpha * flicker),
                    (byte)(180 * alpha * flicker),
                    (byte)(80 * alpha * flicker)
                );

                // Scale - smaller as they fade
                float distScale = MathHelper.Clamp(400f / clipPos.W, 0.3f, 1.5f);
                float finalScale = (0.4f + lifeRatio * 0.4f) * distScale * Constants.RENDER_SCALE;

                spriteBatch.Draw(
                    _sparkTexture,
                    new Vector2(screenX, screenY),
                    null,
                    sparkColor,
                    0f,
                    _textureCenter,
                    finalScale,
                    SpriteEffects.None,
                    0f
                );
            }

            spriteBatch.End();

            // Restore states
            device.BlendState = prevBlend;
            device.DepthStencilState = prevDepth;
        }

        public override void Dispose()
        {
            if (World != null)
            {
                World.Terrain.RemoveDynamicLight(_dynamicLight);
            }
            base.Dispose();
        }
    }
}
