using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class DungeonGateObject : ModelObject
    {
        // A single, static Random instance shared by all DungeonGateObject instances.
        private static readonly Random _random = new Random();

        private class FlameSet
        {
            public List<FireHik01Effect> TopFlames { get; } = new();
            public List<FireHik02Effect> MiddleFlames { get; } = new();
            public List<FireHik03Effect> BaseFlames { get; } = new();
            public List<float> IndividualWindTimes { get; } = new();
            public List<float> IndividualWindStrengths { get; } = new();
            public List<float> ScaleOffsets { get; } = new();
            public Vector3 BasePosition { get; set; }

            // Each flame set gets its own light and unique time offset.
            public float TimeOffset { get; set; }
            public DynamicLight Light { get; } = new DynamicLight
            {
                Color = new Vector3(1.0f, 0.7f, 0.4f), // Warm fire color
                Radius = 400f,
                Intensity = 1.0f
            };
        }

        private FlameSet _firstFlameSet;
        private FlameSet _secondFlameSet;
        private float _baseHeight = 40f;

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

        public DungeonGateObject()
        {
            LightEnabled = true;

            _firstFlameSet = CreateFlameSet();
            _secondFlameSet = CreateFlameSet();

            // Assign a unique, random time offset to each flame set.
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
            Model = await BMDLoader.Instance.Prepare("Object1/DoungeonGate01.bmd");
            await base.Load();

            // Add the dynamic lights to the world when the object is loaded.
            if (World != null)
            {
                World.Terrain.AddDynamicLight(_firstFlameSet.Light);
                World.Terrain.AddDynamicLight(_secondFlameSet.Light);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Type == 55)
            {
                var time = (float)gameTime.TotalGameTime.TotalSeconds;

                _firstFlameSet.BasePosition = BoneTransform[4].Translation;
                _secondFlameSet.BasePosition = BoneTransform[1].Translation;

                // Use the unique time offset for each flame set.
                UpdateFlameSet(_firstFlameSet, time + _firstFlameSet.TimeOffset);
                UpdateFlameSet(_secondFlameSet, time + _secondFlameSet.TimeOffset);
            }
        }

        // --- THE FIX: STEP 5 ---
        // Update now handles the dynamic light as well.
        private void UpdateFlameSet(FlameSet flameSet, float time)
        {
            UpdateIndividualWindEffects(flameSet, time);
            var baseLuminosity = CalculateBaseLuminosity(time);

            UpdateDynamicLight(flameSet, baseLuminosity);
            UpdateFireEffects(flameSet, time, baseLuminosity);
        }

        private void UpdateDynamicLight(FlameSet set, float intensity)
        {
            // The light's position is the gate's world position + the local bone offset.
            set.Light.Position = WorldPosition.Translation + set.BasePosition + new Vector3(0, -5f, _baseHeight);
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
            return 1.0f + (float)(
                Math.Sin(time * 4.0f) * 0.12f +
                Math.Sin(time * 9.0f) * 0.06f +
                Math.Sin(time * 15.0f) * 0.03f
            );
        }

        private float CalculateFlameScale(FlameSet flameSet, int index, float baseScale, float turbulence, float time)
        {
            float smoothRandomFactor = (float)Math.Sin(flameSet.ScaleOffsets[index]) * RANDOM_SCALE_INFLUENCE;
            return baseScale + (turbulence - 1.0f) * 0.2f + smoothRandomFactor;
        }

        private void UpdateBaseFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.BaseFlames[index];
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * (0.8f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.1f));
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(flameSet, index, time, 1.8f, 7.0f);
            flame.Position = flameSet.BasePosition + offset + new Vector3(0f, -5f, _baseHeight);

            flame.Light = BaseFlameColor * flameIntensity * (0.85f + turbulence * 0.15f);
            flame.Scale = CalculateFlameScale(flameSet, index, 1.5f, turbulence, time);
        }

        private void UpdateMiddleFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.MiddleFlames[index];
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * (0.75f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.15f));
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(flameSet, index, time, 2.2f, 6.0f);
            flame.Position = flameSet.BasePosition + offset + new Vector3(0f, -5f, _baseHeight + 18f);

            flame.Light = MiddleFlameColor * flameIntensity * (0.9f + turbulence * 0.1f);
            flame.Scale = CalculateFlameScale(flameSet, index, 2.0f, turbulence, time);
        }

        private void UpdateTopFlame(FlameSet flameSet, int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = flameSet.TopFlames[index];
            var flameIntensity = ClampAlpha(baseLuminosity * turbulence * (0.7f + (float)Math.Sin(flameSet.ScaleOffsets[index]) * 0.2f));
            flame.Alpha = flameIntensity;

            var offset = CalculateIndividualFlameOffset(flameSet, index, time, 2.5f, 5.0f);
            var heightVariation = (float)Math.Sin(time * 2.0f + flameSet.IndividualWindTimes[index]) * 12.0f + flameSet.IndividualWindStrengths[index] * 10.0f;
            flame.Position = flameSet.BasePosition + offset + new Vector3(
                flameSet.IndividualWindStrengths[index] * 4.0f, -5f, _baseHeight + 35f + heightVariation
            );

            flame.Light = TopFlameColor * flameIntensity * (0.95f + turbulence * 0.1f);
            flame.Scale = CalculateFlameScale(flameSet, index, 2.2f, turbulence, time);
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