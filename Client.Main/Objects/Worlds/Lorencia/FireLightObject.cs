using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class FireLightObject : ModelObject
    {
        private static readonly Random _random = new Random();

        // Lists of effects
        private List<FireHik01Effect> _topFlames;
        private List<FireHik02Effect> _middleFlames;
        private List<FireHik03Effect> _baseFlames;
        private DynamicLight _dynamicLight;

        // A unique time offset for this specific fire instance.
        private float _timeOffset;

        private float _baseHeight = 10f;
        private Vector3 _basePosition;

        // Parameters for simulating wind and scale changes
        private List<float> _individualWindTimes;
        private List<float> _individualWindStrengths;
        private List<float> _scaleOffsets;

        // Effect parameter constants
        private const float MIN_ALPHA = 0.6f;
        private const float MAX_ALPHA = 0.8f;
        private const float WIND_CHANGE_SPEED = 0.4f;
        private const float MAX_WIND_STRENGTH = 0.5f;
        private const int FLAME_COUNT = 3;
        private const float SCALE_CHANGE_SPEED = 0.9f;
        private const float RANDOM_SCALE_INFLUENCE = 0.15f;
        private const float OFFSET_Y = -5f;
        private const float MIDDLE_FLAME_ADDITIONAL_Z = 18f;
        private const float TOP_FLAME_ADDITIONAL_Z = 35f;

        // Effect colors
        private readonly Vector3 BaseFlameColor = new Vector3(1.0f, 0.45f, 0.15f);
        private readonly Vector3 MiddleFlameColor = new Vector3(1.0f, 0.65f, 0.25f);
        private readonly Vector3 TopFlameColor = new Vector3(1.0f, 0.75f, 0.35f);

        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<ModelObject>();

        public FireLightObject()
        {
            LightEnabled = true;
            BlendMesh = 2;

            // We use the static random instance to get a unique offset.
            _timeOffset = (float)_random.NextDouble() * 1000f;

            _dynamicLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1f, 0.7f, 0.4f),
                Radius = 250f,
                Intensity = 1f,
                Position = Vector3.Zero
            };

            // Initializing effect lists
            _topFlames = new List<FireHik01Effect>();
            _middleFlames = new List<FireHik02Effect>();
            _baseFlames = new List<FireHik03Effect>();
            _individualWindTimes = new List<float>();
            _individualWindStrengths = new List<float>();
            _scaleOffsets = new List<float>();

            // Initializing parameters for each of the FLAME_COUNT effects
            for (int i = 0; i < FLAME_COUNT; i++)
            {
                _individualWindTimes.Add((float)_random.NextDouble() * MathHelper.TwoPi);
                _individualWindStrengths.Add((float)_random.NextDouble() * MAX_WIND_STRENGTH);
                _scaleOffsets.Add((float)_random.NextDouble() * MathHelper.TwoPi);
            }
        }

        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.FireLight01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/FireLight{idx}.bmd");
            await base.Load();

            if (World != null)
            {
                UpdateDynamicLight(1f);
                World.Terrain.AddDynamicLight(_dynamicLight);
                for (int i = 0; i < FLAME_COUNT; i++)
                {
                    var topFlame = WorldObjectFactory.CreateObject(World, typeof(FireHik01Effect)) as FireHik01Effect;
                    if (topFlame != null) _topFlames.Add(topFlame);

                    var middleFlame = WorldObjectFactory.CreateObject(World, typeof(FireHik02Effect)) as FireHik02Effect;
                    if (middleFlame != null) _middleFlames.Add(middleFlame);

                    var baseFlame = WorldObjectFactory.CreateObject(World, typeof(FireHik03Effect)) as FireHik03Effect;
                    if (baseFlame != null) _baseFlames.Add(baseFlame);
                }
            }
            else
            {
                _logger?.LogDebug("World was not set in FireLightObject during Load.");
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Use the offset time for all calculations.
            float time = (float)gameTime.TotalGameTime.TotalSeconds + _timeOffset;
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (Type == 51)
            {
                UpdateIndividualWindEffects(time, elapsed);
                float baseLuminosity = CalculateBaseLuminosity(time);

                if (WorldPosition.Down.Y != -1)
                    _basePosition = BoneTransform[1].Translation + new Vector3(40f, 30f, 0f);
                else
                    _basePosition = BoneTransform[1].Translation + new Vector3(0f, 0f, 0f);

                UpdateDynamicLight(baseLuminosity);
                UpdateFireEffects(time, baseLuminosity, elapsed);
            }
            else if (Type == 50)
            {
                UpdateIndividualWindEffects(time, elapsed);
                float baseLuminosity = CalculateBaseLuminosity(time);
                _basePosition = BoneTransform[0].Translation + new Vector3(0f, 0f, 150f);
                UpdateDynamicLight(baseLuminosity);
                UpdateFireEffects(time, baseLuminosity, elapsed);
            }
        }

        private void UpdateDynamicLight(float intensity)
        {
            if (World?.Terrain == null) return;
            _dynamicLight.Position = WorldPosition.Translation + _basePosition + new Vector3(0f, OFFSET_Y, _baseHeight);
            _dynamicLight.Intensity = intensity;
        }

        private void UpdateIndividualWindEffects(float time, float elapsed)
        {
            for (int i = 0; i < FLAME_COUNT; i++)
            {
                _individualWindTimes[i] += WIND_CHANGE_SPEED * elapsed * (1 + (float)_random.NextDouble() * 0.2f);
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
                   (float)Math.Sin(time * 5.5f) * 0.2f +
                   (float)Math.Sin(time * 9.0f) * 0.1f;
        }

        private Vector3 CalculateIndividualFlameOffset(int index, float time, float baseFrequency, float amplitude)
        {
            float individualPhase = _individualWindTimes[index];
            Vector3 windOffset = _individualWindStrengths[index] * new Vector3(1f, 0.4f, 0f);

            return new Vector3(
                (float)Math.Sin(time * baseFrequency + individualPhase) * amplitude * (0.8f + (float)_random.NextDouble() * 0.4f),
                (float)Math.Cos(time * baseFrequency * 0.9f + individualPhase) * amplitude * (0.7f + (float)_random.NextDouble() * 0.3f),
                (float)Math.Sin(time * (baseFrequency * 1.1f) + individualPhase) * (amplitude * 0.4f)
            ) + windOffset;
        }

        private void UpdateFireEffects(float time, float baseLuminosity, float elapsed)
        {
            float turbulence = CalculateTurbulence(time);
            for (int i = 0; i < FLAME_COUNT; i++)
            {
                _scaleOffsets[i] += SCALE_CHANGE_SPEED * elapsed;
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
            float turbulenceInfluence = (turbulence - 1.0f) * 0.2f;
            return baseScale + turbulenceInfluence + smoothRandomFactor;
        }

        private void UpdateBaseFlame(int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = _baseFlames[index];
            float individualIntensity = 0.8f + (float)Math.Sin(_scaleOffsets[index]) * 0.1f;
            float flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            Vector3 offset = CalculateIndividualFlameOffset(index, time, 1.8f, 7.0f);
            flame.Position = WorldPosition.Translation + _basePosition + offset + new Vector3(0f, OFFSET_Y, _baseHeight);

            float colorIntensity = 0.85f + turbulence * 0.15f;
            flame.Light = BaseFlameColor * flameIntensity * colorIntensity;
            flame.Scale = CalculateFlameScale(index, 1.5f, turbulence, time);
        }

        private void UpdateMiddleFlame(int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = _middleFlames[index];
            float individualIntensity = 0.75f + (float)Math.Sin(_scaleOffsets[index]) * 0.15f;
            float flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            Vector3 offset = CalculateIndividualFlameOffset(index, time, 2.2f, 6.0f);
            flame.Position = WorldPosition.Translation + _basePosition + offset + new Vector3(0f, OFFSET_Y, _baseHeight + MIDDLE_FLAME_ADDITIONAL_Z);

            float colorIntensity = 0.9f + turbulence * 0.1f;
            flame.Light = MiddleFlameColor * flameIntensity * colorIntensity;
            flame.Scale = CalculateFlameScale(index, 2.0f, turbulence, time);
        }

        private void UpdateTopFlame(int index, float time, float baseLuminosity, float turbulence)
        {
            var flame = _topFlames[index];
            float individualIntensity = 0.7f + (float)Math.Sin(_scaleOffsets[index]) * 0.2f;
            float flameIntensity = ClampAlpha(baseLuminosity * turbulence * individualIntensity);
            flame.Alpha = flameIntensity;

            Vector3 offset = CalculateIndividualFlameOffset(index, time, 2.5f, 5.0f);
            float heightVariation = (float)Math.Sin(time * 2.0f + _individualWindTimes[index]) * 12.0f +
                                    _individualWindStrengths[index] * 10.0f;

            flame.Position = WorldPosition.Translation + _basePosition + offset + new Vector3(
                _individualWindStrengths[index] * 4.0f,
                OFFSET_Y,
                _baseHeight + TOP_FLAME_ADDITIONAL_Z + heightVariation
            );

            float colorIntensity = 0.95f + turbulence * 0.1f;
            flame.Light = TopFlameColor * flameIntensity * colorIntensity;
            flame.Scale = CalculateFlameScale(index, 2.2f, turbulence, time);
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

                if (_topFlames != null)
                {
                    foreach (var flame in _topFlames) World.RemoveObject(flame);
                    foreach (var flame in _middleFlames) World.RemoveObject(flame);
                    foreach (var flame in _baseFlames) World.RemoveObject(flame);
                }
            }
            base.Dispose();
        }
    }
}