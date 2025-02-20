using System.Runtime.InteropServices;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BMDTriangle
    {
        public byte Polygon;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public short[] VertexIndex;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public short[] NormalIndex;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public short[] TexCoordIndex;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public BMDTexCoord[] LightMapCoord;

        public short LightMapIndexes;
    }
}
