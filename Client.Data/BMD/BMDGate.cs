using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct BMDGate
    {
        public byte Flag;
        public byte Map;
        public byte X1;
        public byte Y1;
        public byte X2;
        public byte Y2;
        public ushort Target;
        public byte Angle;
        public ushort Level;
        public ushort MaxLevel;
    }
}
