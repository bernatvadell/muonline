using System.Drawing;
using System.Numerics;

namespace Client.Data.OZB
{
    public class OZB
    {
        public byte Version { get; set; }
        public Color[] Data { get; set; } = [];
    }
}
