using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Core.Utilities
{
    public static class MathUtils
    {
        public static Matrix AngleMatrix(Vector3 angles)
        {
            // Convert angles to radians
            float yaw = MathHelper.ToRadians(angles.Y);
            float pitch = MathHelper.ToRadians(angles.X);
            float roll = MathHelper.ToRadians(angles.Z);

            // Create rotation matrix
            Matrix rotationMatrix = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);

            // Transpose the matrix only once
            return Matrix.Transpose(rotationMatrix);
        }

        public static float DotProduct(Vector3 x, Vector3 y)
        {
            return Vector3.Dot(x, y);
        }

        public static Vector3 FaceNormalize(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            // Minimal optimization by eliminating intermediate variables
            float nx = (v2.Y - v1.Y) * (v3.Z - v1.Z) - (v3.Y - v1.Y) * (v2.Z - v1.Z);
            float ny = (v2.Z - v1.Z) * (v3.X - v1.X) - (v3.Z - v1.Z) * (v2.X - v1.X);
            float nz = (v2.X - v1.X) * (v3.Y - v1.Y) - (v3.X - v1.X) * (v2.Y - v1.Y);

            // Use LengthSquared to check for zero length
            float lengthSquared = nx * nx + ny * ny + nz * nz;
            if (lengthSquared == 0) return Vector3.Zero;

            float invLength = 1.0f / (float)Math.Sqrt(lengthSquared);
            return new Vector3(nx * invLength, ny * invLength, nz * invLength);
        }

        public static Vector3 VectorRotate(Vector3 in1, Matrix in2)
        {
            return new Vector3(
                in1.X * in2.M11 + in1.Y * in2.M21 + in1.Z * in2.M31,
                in1.X * in2.M12 + in1.Y * in2.M22 + in1.Z * in2.M32,
                in1.X * in2.M13 + in1.Y * in2.M23 + in1.Z * in2.M33
            );
        }

        public static Vector3 VectorIRotate(Vector3 in1, Matrix in2)
        {
            return new Vector3(
                in1.X * in2.M11 + in1.Y * in2.M21 + in1.Z * in2.M31,
                in1.X * in2.M12 + in1.Y * in2.M22 + in1.Z * in2.M32,
                in1.X * in2.M13 + in1.Y * in2.M23 + in1.Z * in2.M33
            );
        }

        public static Quaternion AngleQuaternion(Vector3 angles)
        {
            float halfRoll = angles.Z * 0.5f;
            float halfYaw = angles.Y * 0.5f;
            float halfPitch = angles.X * 0.5f;

            float sr = (float)Math.Sin(halfPitch);
            float cr = (float)Math.Cos(halfPitch);
            float sp = (float)Math.Sin(halfYaw);
            float cp = (float)Math.Cos(halfYaw);
            float sy = (float)Math.Sin(halfRoll);
            float cy = (float)Math.Cos(halfRoll);

            float x = sr * cp * cy - cr * sp * sy;
            float y = cr * sp * cy + sr * cp * sy;
            float z = cr * cp * sy - sr * sp * cy;
            float w = cr * cp * cy + sr * sp * sy;

            return new Quaternion(x, y, z, w);
        }
    }
}
