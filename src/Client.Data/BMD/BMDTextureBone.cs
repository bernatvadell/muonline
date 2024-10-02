using System;
using System.IO;

namespace Client.Data.BMD
{
    public class BMDTextureBone
    {
        public static BMDTextureBone Dummy = new() { Name = "Dummy" };

        public string Name { get; set; } = string.Empty;
        public short Parent { get; set; } = 0;
        public BMDBoneMatrix[] Matrixes { get; set; } = [];
    }
}
