using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main
{
    public static class MathUtils
    {
        public static Matrix AngleMatrix(Vector3 angles)
        {
            float yaw = MathHelper.ToRadians(angles.Y);  
            float pitch = MathHelper.ToRadians(angles.X); 
            float roll = MathHelper.ToRadians(angles.Z); 
            Matrix rotationMatrix = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);
            return Matrix.Transpose(rotationMatrix);
        }

        public static float DotProduct(Vector3 x, Vector3 y) => (x.X * y.X) + (x.Y * y.Y) + (x.Z * y.Z);

        public static Vector3 FaceNormalize(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            float nx, ny, nz;
            nx = (v2.Y - v1.Y) * (v3.Z - v1.Z) - (v3.Y - v1.Y) * (v2.Z - v1.Z);
            ny = (v2.Z - v1.Z) * (v3.X - v1.X) - (v3.Z - v1.Z) * (v2.X - v1.X);
            nz = (v2.X - v1.X) * (v3.Y - v1.Y) - (v3.X - v1.X) * (v2.Y - v1.Y);
            double dot = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (dot == 0) return Vector3.Zero;
            return new Vector3(nx / (float)dot, ny / (float)dot, nz / (float)dot);
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

        public static float LerpAngle(float from, float to, float t)
        {
            float difference = MathHelper.WrapAngle(to - from);
            return from + difference * t;
        }

    }
}
