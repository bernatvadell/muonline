using System;
using Microsoft.Xna.Framework;

namespace Client.Main.Graphics
{
    /// <summary>
    /// Manages the day-night cycle by calculating sun direction based on real time.
    /// The sun moves across the sky simulating realistic day/night transitions.
    /// </summary>
    public static class SunCycleManager
    {
        // Cached values for ambient/strength modulation
        private static float _currentTimeOfDay; // 0-24 hours
        private static float _sunAltitude; // -1 to 1 (negative = below horizon)
        private static Vector3 _baseSunDirection;

        /// <summary>
        /// Returns the original (configured) sun direction, normalized and unaffected by runtime day-night updates.
        /// Use this for static/baked lighting that should not depend on current time-of-day.
        /// </summary>
        public static Vector3 BaseSunDirection => _baseSunDirection;

        /// <summary>
        /// Current time of day in hours (0-24).
        /// </summary>
        public static float CurrentTimeOfDay => _currentTimeOfDay;

        /// <summary>
        /// Current sun altitude (-1 = midnight nadir, 0 = horizon, 1 = noon zenith).
        /// </summary>
        public static float SunAltitude => _sunAltitude;

        /// <summary>
        /// Whether it's currently night (sun below horizon).
        /// </summary>
        public static bool IsNight => _sunAltitude < 0;

        /// <summary>
        /// Ambient light multiplier based on time of day (0.2 at night, 1.0 at noon).
        /// </summary>
        public static float AmbientMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Sun strength multiplier based on time of day (0 at night, 1.0 at noon).
        /// </summary>
        public static float SunStrengthMultiplier { get; private set; } = 1f;

        static SunCycleManager()
        {
            // Store the initial sun direction as base reference
            _baseSunDirection = Constants.SUN_DIRECTION;
            if (_baseSunDirection.LengthSquared() < 0.001f)
                _baseSunDirection = new Vector3(-1f, 0f, -1f);
            _baseSunDirection.Normalize();
        }

        /// <summary>
        /// Updates the sun position based on real-world time.
        /// Call this every frame when day-night cycle is enabled.
        /// </summary>
        public static void Update()
        {
            if (!Constants.ENABLE_DAY_NIGHT_CYCLE)
                return;

            // Get current real time
            DateTime now = DateTime.Now;
            float realHours = (float)now.TimeOfDay.TotalHours;

            // Apply speed multiplier (1 = real-time, higher = faster)
            // For multiplied time, we use elapsed time from midnight
            float gameHours = realHours * Constants.DAY_NIGHT_SPEED_MULTIPLIER;
            _currentTimeOfDay = gameHours % 24f;

            // Calculate sun position on a circular arc
            // 6:00 = sunrise (east), 12:00 = noon (zenith), 18:00 = sunset (west), 0:00 = midnight (nadir)
            // Using a simple sinusoidal model for sun altitude

            // Convert hours to radians: 0h = -PI/2 (nadir), 6h = 0 (rise), 12h = PI/2 (zenith), 18h = PI (set)
            float hourAngle = (_currentTimeOfDay - 6f) / 24f * MathHelper.TwoPi;

            // Sun altitude: sin curve with peak at noon (12:00)
            _sunAltitude = MathF.Sin(hourAngle);

            // Sun azimuth: rotates around the horizon
            // At 6:00 sun rises in east (+X), at 12:00 it's south/overhead, at 18:00 it sets in west (-X)
            float azimuthAngle = (_currentTimeOfDay - 6f) / 12f * MathHelper.Pi;

            // Calculate sun direction vector
            // X: east-west movement (positive = east, negative = west)
            // Y: north-south (keep minimal for simplicity)
            // Z: up-down (altitude)
            float horizontalComponent = MathF.Cos(hourAngle);
            float sunX = MathF.Cos(azimuthAngle) * Math.Max(0.3f, MathF.Abs(horizontalComponent));
            float sunY = MathF.Sin(azimuthAngle) * 0.3f; // Slight north-south movement
            float sunZ = -_sunAltitude; // Negative because sun direction points FROM light TO scene

            Vector3 newSunDir = new Vector3(sunX, sunY, sunZ);
            if (newSunDir.LengthSquared() > 0.001f)
            {
                newSunDir.Normalize();
                Constants.SUN_DIRECTION = newSunDir;
            }

            // Calculate lighting multipliers based on sun altitude
            // Smooth transition using smoothstep-like curve
            if (_sunAltitude > 0.1f)
            {
                // Daytime
                SunStrengthMultiplier = 1f;
                AmbientMultiplier = 1f;
            }
            else if (_sunAltitude > -0.1f)
            {
                // Dawn/Dusk transition (-0.1 to 0.1)
                float t = (_sunAltitude + 0.1f) / 0.2f; // 0 to 1
                t = t * t * (3f - 2f * t); // Smoothstep
                SunStrengthMultiplier = t;
                AmbientMultiplier = 0.5f + 0.5f * t; // 0.5 at night, 1.0 at day
            }
            else
            {
                // Night
                SunStrengthMultiplier = 0f;
                AmbientMultiplier = 0.5f;
            }
        }

        /// <summary>
        /// Gets the effective sun strength considering time of day.
        /// </summary>
        public static float GetEffectiveSunStrength()
        {
            if (!Constants.ENABLE_DAY_NIGHT_CYCLE)
                return Constants.SUN_STRENGTH;

            return Constants.SUN_STRENGTH * SunStrengthMultiplier;
        }

        /// <summary>
        /// Gets the effective shadow strength considering time of day.
        /// </summary>
        public static float GetEffectiveShadowStrength()
        {
            if (!Constants.ENABLE_DAY_NIGHT_CYCLE)
                return Constants.SUN_SHADOW_STRENGTH;

            return Constants.SUN_SHADOW_STRENGTH * SunStrengthMultiplier;
        }

        /// <summary>
        /// Gets a debug string showing current time info.
        /// </summary>
        public static string GetDebugInfo()
        {
            if (!Constants.ENABLE_DAY_NIGHT_CYCLE)
                return "Day-Night: Off";

            int hours = (int)_currentTimeOfDay;
            int minutes = (int)((_currentTimeOfDay - hours) * 60);
            string period = IsNight ? "Night" : "Day";
            return $"Time: {hours:D2}:{minutes:D2} ({period}) Alt:{_sunAltitude:F2}";
        }

        /// <summary>
        /// Resets the sun direction to its original value when cycle is disabled.
        /// </summary>
        public static void ResetToDefault()
        {
            Constants.SUN_DIRECTION = _baseSunDirection;
            SunStrengthMultiplier = 1f;
            AmbientMultiplier = 1f;
        }
    }
}
