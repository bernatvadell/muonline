using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
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
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Object1/Bonfire01.bmd");
            await base.Load();

            // Add the dynamic light to the world when the object is loaded.
            if (World != null)
            {
                World.Terrain.AddDynamicLight(_dynamicLight);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Use the offset time for all calculations to desynchronize.
            var time = (float)gameTime.TotalGameTime.TotalSeconds + _timeOffset;

            UpdateIndividualWindEffects(time);
            var baseLuminosity = CalculateBaseLuminosity(time);
            _basePosition = BoneTransform[3].Translation;

            // Update both the visual effects and the dynamic light.
            UpdateDynamicLight(baseLuminosity);
            UpdateFireEffects(time, baseLuminosity);
        }

        private void UpdateDynamicLight(float intensity)
        {
            // The light's position must be in absolute world coordinates.
            _dynamicLight.Position = WorldPosition.Translation + _basePosition + new Vector3(0f, 0f, _baseHeight);
            _dynamicLight.Intensity = intensity;
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