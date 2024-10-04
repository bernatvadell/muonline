using System.Numerics;

namespace Client.Data
{
    public static class MathUtils
    {
        public static Quaternion AngleQuaternion(Vector3 angles)
        {
            float angle;
            float sr, sp, sy, cr, cp, cy;

            angle = angles.Z * 0.5f;
            sy = (float)Math.Sin(angle);
            cy = (float)Math.Cos(angle);
            angle = angles.Y * 0.5f;
            sp = (float)Math.Sin(angle);
            cp = (float)Math.Cos(angle);
            angle = angles.X * 0.5f;
            sr = (float)Math.Sin(angle);
            cr = (float)Math.Cos(angle);

            float x = sr * cp * cy - cr * sp * sy;
            float y = cr * sp * cy + sr * cp * sy;
            float z = cr * cp * sy - sr * sp * cy;
            float w = cr * cp * cy + sr * sp * sy;

            return new Quaternion(x, y, z, w);
        }
    }
}
