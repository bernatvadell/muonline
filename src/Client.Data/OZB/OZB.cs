using System.Drawing;
using System.Numerics;

namespace Client.Data.OZB
{
    public class OZB
    {
        public byte Version { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Color[] Data { get; set; } = [];
    }
}
