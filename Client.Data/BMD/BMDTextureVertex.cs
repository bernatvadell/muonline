using System.Numerics;
using System.Runtime.InteropServices;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BMDTextureVertex
    {
        public short Node;
        public Vector3 Position;

        public override string ToString()
        {
            return $"Node: {Node}, Position: {Position.ToString()}";
        }
    }
}
