using Client.Main.Content;
using Client.Main.Controls;
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
            public List<FireHik01Effect> TopFlames { get; } = new();
            public List<FireHik02Effect> MiddleFlames { get; } = new();
            public List<FireHik03Effect> BaseFlames { get; } = new();
            public List<float> IndividualWindTimes { get; } = new();
            public List<float> IndividualWindStrengths { get; } = new();
            public List<float> ScaleOffsets { get; } = new();
            public Vector3 BasePosition { get; set; }
            public float TimeOffset { get; set; }

            public DynamicLight Light { get; } = new DynamicLight
            {
                Color = new Vector3(1f, 0.7f, 0.4f),
                Radius = 400f,
                Intensity = 1f
            };
        }

        private FlameSet _firstFlameSet;
        private FlameSet _secondFlameSet;
        private float _baseHeight = 40f;
        private static readonly Random _random = new Random();

        // Constants
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

            _firstFlameSet = CreateFlameSet();
            _secondFlameSet = CreateFlameSet();

            _firstFlameSet.TimeOffset = (float)_random.NextDouble() * 1000f;
            _secondFlameSet.TimeOffset = (float)_random.NextDouble() * 1000f;
        }

        private FlameSet CreateFlameSet()
        {
            var flameSet = new FlameSet();

            flameSet.Light.Owner = this;

            for (int i = 0; i < FLAME_COUNT; i++)
            {
                var topFlame = new FireHik01Effect();
                var middleFlame = new FireHik02Effect();
                var baseFlame = new FireHik03Effect();

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
            Model = await BMDLoader.Instance.Prepare("Object1/Bridge01.bmd");
            await base.Load();

            if (World != null)
            {
                World.Terrain.AddDynamicLight(_firstFlameSet.Light);
                World.Terrain.AddDynamicLight(_secondFlameSet.Light);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Type == 80)
            {
                float time = (float)gameTime.TotalGameTime.TotalSeconds;

                _firstFlameSet.BasePosition = BoneTransform[0].Translation + new Vector3(100f, 210f, 20f);
                _secondFlameSet.BasePosition = BoneTransform[0].Translation + new Vector3(100f, -210f, 20f);

                UpdateFlameSet(_firstFlameSet, time + _firstFlameSet.TimeOffset);
                UpdateFlameSet(_secondFlameSet, time + _secondFlameSet.TimeOffset);
            }
        }

        public override void DrawMesh(int mesh)
        {
            if (Type == 80 && mesh == 1) return;
            base.DrawMesh(mesh);
        }

        private void UpdateFlameSet(FlameSet flameSet, float time)
        {
            UpdateIndividualWindEffects(flameSet, time);
            float baseLuminosity = CalculateBaseLuminosity(time);
            UpdateDynamicLight(flameSet, baseLuminosity);
            UpdateFireEffects(flameSet, time, baseLuminosity);
        }

        private void UpdateDynamicLight(FlameSet set, float intensity)
        {
            set.Light.Position = WorldPosition.Translation + set.BasePosition + new Vector3(0f, -5f, _baseHeight);
            set.Light.Intensity = intensity;
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
            return 0.9f + (float)Math.Sin(time * 5.5f) * 0.2f + (float)Math.Sin(time * 9.0f) * 0.1f;
        }

        private Vector3 CalculateIndividualFlameOffset(FlameSet flameSet, int index, float time, float baseFrequency, float amplitude)
        {
            float individualPhase = flameSet.IndividualWindTimes[index];
            Vector3 windOffset = flameSet.IndividualWindStrengths[index] * new Vector3(1f, 0.4f, 0f);

            return new Vector3(
                (float)Math.Sin(time * baseFrequency + individualPhase) * amplitude * (0.8f + (float)_random.NextDouble() * 0.2f),
                (float)Math.Cos(time * baseFrequency * 0.9f + individualPhase) * amplitude * (0.7f + (float)_random.NextDouble() * 0.2f),
                (float)Math.Sin(time * (baseFrequency * 1.1f) + individualPhase) * (amplitude * 0.3f)
            ) + windOffset;
        }

        private void UpdateFireEffects(FlameSet flameSet, float time, float baseLuminosity)
        {
            float turbulence = CalculateTurbulence(time);
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
            return 1.0f + (float)(Math.Sin(time * 4.0f) * 0.12f + Math.Sin(time * 9.0f) * 0.06f + Math.Sin(time * 15.0f) * 0.03f);
        }

        private float CalculateFlameScale(FlameSet flameSet, int index, float baseScale, float turbulence, float time)
        {
            float smoothRandomFactor = (float)Math.Sin(flameSet.ScaleOffsets[index]) * RANDOM_SCALE_INFLUENCE;
            return baseScale + (turbulence - 1.0f) * 0.2f + smoothRandomFactor;
        }

        private void UpdateBaseFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.BaseFlames[index];
            float flicker = 0.98f + 0.05f * (float)Math.Sin(time * (1.8f + flameSet.IndividualWindTimes[index]));
            float flameIntensity = ClampAlpha(baseLuminosity * turbulence * (0.8f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.1f) * flicker);
            flame.Alpha = flameIntensity;

            Vector3 offset = CalculateIndividualFlameOffset(flameSet, index, time, 1.8f, 7.0f);
            Vector3 driftOffset = new Vector3((float)Math.Sin(time * 0.2f + flameSet.IndividualWindTimes[index]), (float)Math.Cos(time * 0.2f + flameSet.IndividualWindTimes[index]), 0f) * 1.5f;
            flame.Position = flameSet.BasePosition + offset + driftOffset + new Vector3(0f, -5f, _baseHeight);

            flame.Light = BaseFlameColor * flameIntensity * (0.85f + turbulence * 0.15f);
            flame.Scale = CalculateFlameScale(flameSet, index, 1.5f, turbulence, time) * (0.98f + 0.05f * (float)Math.Sin(time * 2.5f + flameSet.ScaleOffsets[index]));
        }

        private void UpdateMiddleFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.MiddleFlames[index];
            float flicker = 0.98f + 0.05f * (float)Math.Sin(time * (2.2f + flameSet.IndividualWindTimes[index]));
            float flameIntensity = ClampAlpha(baseLuminosity * turbulence * (0.75f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.15f) * flicker);
            flame.Alpha = flameIntensity;

            Vector3 offset = CalculateIndividualFlameOffset(flameSet, index, time, 2.2f, 6.0f);
            Vector3 driftOffset = new Vector3((float)Math.Sin(time * 0.2f + flameSet.IndividualWindTimes[index] + 0.5f), (float)Math.Cos(time * 0.2f + flameSet.IndividualWindTimes[index] + 0.5f), 0f) * 1.5f;
            flame.Position = flameSet.BasePosition + offset + driftOffset + new Vector3(0f, -5f, _baseHeight + 18f);

            flame.Light = MiddleFlameColor * flameIntensity * (0.9f + turbulence * 0.1f);
            flame.Scale = CalculateFlameScale(flameSet, index, 2.0f, turbulence, time) * (0.98f + 0.05f * (float)Math.Cos(time * 2.8f + flameSet.ScaleOffsets[index]));
        }

        private void UpdateTopFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.TopFlames[index];
            float flicker = 0.98f + 0.02f * (float)Math.Sin(time * (2.5f + flameSet.IndividualWindTimes[index]));
            float flameIntensity = ClampAlpha(baseLuminosity * turbulence * (0.7f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.2f) * flicker);
            flame.Alpha = flameIntensity;

            Vector3 offset = CalculateIndividualFlameOffset(flameSet, index, time, 2.5f, 5.0f);
            float heightVariation = (float)Math.Sin(time * 2.0f + flameSet.IndividualWindTimes[index]) * 12.0f + flameSet.IndividualWindStrengths[index] * 10.0f;
            Vector3 driftOffset = new Vector3((float)Math.Sin(time * 0.2f + flameSet.IndividualWindTimes[index] + 1.0f), (float)Math.Cos(time * 0.2f + flameSet.IndividualWindTimes[index] + 1.0f), 0f) * 1.5f;
            flame.Position = flameSet.BasePosition + offset + driftOffset + new Vector3(flameSet.IndividualWindStrengths[index] * 4.0f, -5f, _baseHeight + 35f + heightVariation);

            flame.Light = TopFlameColor * flameIntensity * (0.95f + turbulence * 0.1f);
            flame.Scale = CalculateFlameScale(flameSet, index, 2.2f, turbulence, time) * (0.98f + 0.05f * (float)Math.Sin(time * 3.0f + flameSet.ScaleOffsets[index]));
        }

        private float ClampAlpha(float alpha)
        {
            return Math.Max(MIN_ALPHA, Math.Min(MAX_ALPHA, alpha));
        }

        public override void Dispose()
        {
            if (World != null)
            {
                World.Terrain.RemoveDynamicLight(_firstFlameSet.Light);
                World.Terrain.RemoveDynamicLight(_secondFlameSet.Light);
            }
            base.Dispose();
        }
    }
}