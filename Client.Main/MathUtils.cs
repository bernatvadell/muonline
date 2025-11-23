using Microsoft.Xna.Framework;
using System;

namespace Client.Main
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
            var edge1 = v2 - v1;
            var edge2 = v3 - v1;
            var normal = Vector3.Cross(edge1, edge2);

            return normal.LengthSquared() <= float.Epsilon
                ? Vector3.Zero
                : Vector3.Normalize(normal);
        }

        public static Vector3 VectorRotate(Vector3 in1, Matrix in2)
        {
            return Vector3.Transform(in1, in2);
        }

        public static Vector3 VectorIRotate(Vector3 in1, Matrix in2)
        {
            return Vector3.Transform(in1, in2);
        }

        public static Quaternion AngleQuaternion(Vector3 angles)
        {
            return Quaternion.CreateFromYawPitchRoll(angles.Y, angles.X, angles.Z);
        }
    }
}
