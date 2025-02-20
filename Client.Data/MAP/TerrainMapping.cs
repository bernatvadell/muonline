using System.Runtime.InteropServices;

namespace Client.Data.MAP
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TerrainMapping
    {
        public byte Version;
        public byte MapNumber;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)]
        public byte[] Layer1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)]
        public byte[] Layer2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)]
        public byte[] Alpha;
    }
}
