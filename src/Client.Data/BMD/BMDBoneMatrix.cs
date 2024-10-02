
using System.Numerics;
using System.Runtime.InteropServices;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BMDBoneMatrix
    {
        public Vector3[] Position { get; set; }
        public Vector3[] Rotation { get; set; }
    }
}
