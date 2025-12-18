using System;
using Microsoft.Xna.Framework;

namespace Client.Main.Core.Utilities
{
    public static class AngleUtils
    {
        public static float NormalizeDegrees360(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            return degrees;
        }

        /// <summary>
        /// Returns a 0..360 degree angle from (x1,y1) to (x2,y2) matching the original MU client CreateAngle():
        /// 0 = up, 90 = right, 180 = down, 270 = left (screen space, Y grows downward).
        /// </summary>
        public static float CreateAngleDegrees(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;

            // Bearing-style angle (north-up reference), equivalent to the SourceMain5.2 CreateAngle implementation.
            float deg = MathHelper.ToDegrees(MathF.Atan2(dx, -dy));
            return NormalizeDegrees360(deg);
        }

        public static float WrapAngle(float radians)
        {
            return MathHelper.WrapAngle(radians);
        }
    }
}
