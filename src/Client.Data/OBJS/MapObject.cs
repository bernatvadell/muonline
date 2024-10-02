using System.Numerics;
using System.Runtime.InteropServices;

namespace Client.Data.OBJS
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MapObject
    {
        public ushort Type;
        public Vector3 Position;
        public Vector3 Angle;
        public float Scale;
    }
}
