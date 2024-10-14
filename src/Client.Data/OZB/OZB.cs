using System.Numerics;

namespace Client.Data.OZB
{
    public class OZB
    {
        public byte Version { get; set; }
        public byte[] Data { get; set; } = [];
    }
}
