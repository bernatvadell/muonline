using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class BridgeObject : ModelObject
    {
        private class FlameSet
        {
            public List<FireHik01Effect> TopFlames { get; set; }
            public List<FireHik02Effect> MiddleFlames { get; set; }
            public List<FireHik03Effect> BaseFlames { get; set; }
            public List<float> IndividualWindTimes { get; set; }
            public List<float> IndividualWindStrengths { get; set; }
            public List<float> ScaleOffsets { get; set; }
            public Vector3 BasePosition { get; set; }

            public FlameSet()
            {
                TopFlames = new List<FireHik01Effect>();
                MiddleFlames = new List<FireHik02Effect>();
                BaseFlames = new List<FireHik03Effect>();
                IndividualWindTimes = new List<float>();
                IndividualWindStrengths = new List<float>();
                ScaleOffsets = new List<float>();
            }
        }

        private FlameSet _firstFlameSet;
        private FlameSet _secondFlameSet;
        private float _baseHeight = 40f;
        private Random _random;

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

        public BridgeObject()
        {
            LightEnabled = true;
            _random = new Random();

            _firstFlameSet = CreateFlameSet();
            _secondFlameSet = CreateFlameSet();
        }

        private FlameSet CreateFlameSet()
        {
            var flameSet = new FlameSet();

            for (int i = 0; i < FLAME_COUNT; i++)
            {
                var topFlame = new FireHik01Effect { };
                var middleFlame = new FireHik02Effect { };
                var baseFlame = new FireHik03Effect { };

                Children.Add(topFlame);
                Children.Add(middleFlame);
                Children.Add(baseFlame);

                flameSet.TopFlames.Add(topFlame);
                flameSet.MiddleFlames.Add(middleFlame);
                flameSet.BaseFlames.Add(baseFlame);

                flameSet.IndividualWindTimes.Add((float)_random.NextDouble() * MathHelper.TwoPi);
                flameSet.IndividualWindStrengths.Add((float)_random.NextDouble() * MAX_WIND_STRENGTH);
                flameSet.ScaleOffsets.Add((float)_random.NextDouble() * MathHelper.TwoPi);
            }

            return flameSet;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Bridge01.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Type == 80)
            {
                var time = (float)gameTime.TotalGameTime.TotalSeconds;

                _firstFlameSet.BasePosition = BoneTransform[0].Translation + new Vector3(100f, 210f, 20f);
                _secondFlameSet.BasePosition = BoneTransform[0].Translation + new Vector3(100f, -210f, 20f);

                UpdateFlameSet(_firstFlameSet, time);
                UpdateFlameSet(_secondFlameSet, time);
            }
        }

        public override void DrawMesh(int mesh)
        {
            if (Type == 80 && mesh == 1)
            {
                return;
            }

            base.DrawMesh(mesh);
        }

        private void UpdateFlameSet(FlameSet flameSet, float time)
        {
            UpdateIndividualWindEffects(flameSet, time);
            var baseLuminosity = CalculateBaseLuminosity(time);
            UpdateFireEffects(flameSet, time, baseLuminosity);
        }

        private void UpdateIndividualWindEffects(FlameSet flameSet, float time)
        {
            for (int i = 0; i < FLAME_COUNT; i++)
            {
                flameSet.IndividualWindTimes[i] += WIND_CHANGE_SPEED * 0.016f * (1 + (float)_random.NextDouble() * 0.2f);
                flameSet.IndividualWindStrengths[i] = CalculateIndividualWindStrength(flameSet, i, time);
            }
        }

        private float CalculateIndividualWindStrength(FlameSet flameSet, int index, float time)
        {
            float baseWind = (float)Math.Sin(flameSet.IndividualWindTimes[index]);
            float randomOffset = (float)Math.Sin(time * (1.5f + index * 0.2f));
            return (baseWind * 0.7f + randomOffset * 0.3f) * MAX_WIND_STRENGTH;
        }

        private float CalculateBaseLuminosity(float time)
        {
            return 0.9f +
                   (float)Math.Sin(time * 1.8f) * 0.15f +
                   (float)Math.Sin(time * 3.7f) * 0.08f;
        }

        private Vector3 CalculateIndividualFlameOffset(FlameSet flameSet, int index, float time, float baseFrequency, float amplitude)
        {
            float individualPhase = flameSet.IndividualWindTimes[index];
            var windOffset = flameSet.IndividualWindStrengths[index] * new Vector3(1f, 0.4f, 0f);

            return new Vector3(
                (float)Math.Sin(time * baseFrequency + individualPhase) * amplitude * (0.8f + (float)_random.NextDouble() * 0.4f),
                (float)Math.Cos(time * baseFrequency * 0.9f + individualPhase) * amplitude * (0.7f + (float)_random.NextDouble() * 0.3f),
                (float)Math.Sin(time * (baseFrequency * 1.1f) + individualPhase) * (amplitude * 0.4f)
            ) + windOffset;
        }

        private void UpdateFireEffects(FlameSet flameSet, float time, float baseLuminosity)
        {
            var turbulence = CalculateTurbulence(time);

            for (int i = 0; i < FLAME_COUNT; i++)
            {
                flameSet.ScaleOffsets[i] += SCALE_CHANGE_SPEED * 0.016f;

                UpdateBaseFlame(flameSet, i, time, baseLuminosity, turbulence);
                UpdateMiddleFlame(flameSet, i, time, baseLuminosity, turbulence);
                UpdateTopFlame(flameSet, i, time, baseLuminosity, turbulence);
            }
        }

        private float CalculateTurbulence(float time)
        {
            return (float)(
                Math.Sin(time * 4.0f) * 0.12f +
                Math.Sin(time * 9.0f) * 0.06f +
                Math.Sin(time * 15.0f) * 0.03f
            ) + 1.0f;
        }

        private float CalculateFlameScale(FlameSet flameSet, int index, float baseScale, float turbulence, float time)
        {
            float smoothRandomFactor = (float)Math.Sin(flameSet.ScaleOffsets[index]) * RANDOM_SCALE_INFLUENCE;
            float turbulenceInfluence = (turbulence - 1.0f) * 0.2f;
            return baseScale + turbulenceInfluence + smoothRandomFactor;
        }

        private void UpdateBaseFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.BaseFlames[index];
            float individualIntensity = 0.8f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.1f;
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(flameSet, index, time, 1.8f, 7.0f);
            flame.Position = flameSet.BasePosition + offset + new Vector3(0f, -5f, _baseHeight);

            var colorIntensity = 0.85f + turbulence * 0.15f;
            flame.Light = BaseFlameColor * flameIntensity * colorIntensity;

            flame.Scale = CalculateFlameScale(flameSet, index, 1.5f, turbulence, time);
        }

        private void UpdateMiddleFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.MiddleFlames[index];
            float individualIntensity = 0.75f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.15f;
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(flameSet, index, time, 2.2f, 6.0f);
            flame.Position = flameSet.BasePosition + offset + new Vector3(0f, -5f, _baseHeight + 18f);

            var colorIntensity = 0.9f + turbulence * 0.1f;
            flame.Light = MiddleFlameColor * flameIntensity * colorIntensity;

            flame.Scale = CalculateFlameScale(flameSet, index, 2.0f, turbulence, time);
        }

        private void UpdateTopFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.TopFlames[index];
            float individualIntensity = 0.7f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.2f;
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(flameSet, index, time, 2.5f, 5.0f);
            var heightVariation = (float)Math.Sin(time * 2.0f + flameSet.IndividualWindTimes[index]) * 12.0f +
                                flameSet.IndividualWindStrengths[index] * 10.0f;

            flame.Position = flameSet.BasePosition + offset + new Vector3(
                flameSet.IndividualWindStrengths[index] * 4.0f,
                -5f,
                _baseHeight + 35f + heightVariation
            );

            var colorIntensity = 0.95f + turbulence * 0.1f;
            flame.Light = TopFlameColor * flameIntensity * colorIntensity;

            flame.Scale = CalculateFlameScale(flameSet, index, 2.2f, turbulence, time);
        }

        private float ClampAlpha(float alpha)
        {
            return Math.Max(MIN_ALPHA, Math.Min(MAX_ALPHA, alpha));
        }
    }
}