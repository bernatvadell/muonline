using System.Numerics;
using System.Runtime.InteropServices;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BMDTextureNormal
    {
        public short Node;
        public Vector3 Normal;
        public short BindVertex;

        public override string ToString()
        {
            return $"Node: {Node}, Normal: {Normal.ToString()}, BindVertex: {BindVertex}";
        }
    }
}
