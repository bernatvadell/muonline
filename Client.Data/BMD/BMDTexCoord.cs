using System.Runtime.InteropServices;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BMDTexCoord
    {
        public float U;
        public float V;

        public override string ToString()
        {
            return $"U: {U}, V: {V}";
        }
    }
}
