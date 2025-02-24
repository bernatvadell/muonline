using System.Runtime.InteropServices;

namespace Client.Data.CWS
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct WayPoint
    {
        public int Index;
        public float CameraX;
        public float CameraY;
        public float CameraZ;
        public int Delay;
        public float CameraMoveAccel;
        public float CameraDistanceLevel;
    }
}
