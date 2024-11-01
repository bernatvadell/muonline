using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.Texture
{
    public class OZTReader : BaseReader<TextureData>
    {
        public const int MAX_WIDTH = 1024;
        public const int MAX_HEIGHT = 1024;
        private const int HEADER_SIZE = 16;
        private const string INVALID_OZT_MESSAGE = "Invalid OZT file";
        private const int COMPONENTS = 4;

        protected override TextureData Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));
            br.BaseStream.Seek(HEADER_SIZE, SeekOrigin.Begin);

            short nx = br.ReadInt16();
            short ny = br.ReadInt16();
            byte depth = br.ReadByte();
            byte u1 = br.ReadByte();
            if (depth != 32 || nx > MAX_WIDTH || ny > MAX_HEIGHT)
                throw new FileLoadException(INVALID_OZT_MESSAGE);

            int width = GetNearestPowerOfTwo(nx);
            int height = GetNearestPowerOfTwo(ny);
            var data = new byte[width * height * COMPONENTS];

            for (int y = 0; y < ny; y++)
            {
                int dstIndex = (ny - 1 - y) * width * COMPONENTS;
                for (int x = 0; x < nx; x++)
                {
                    data[dstIndex + 2] = br.ReadByte();    // Red
                    data[dstIndex + 1] = br.ReadByte();    // Green
                    data[dstIndex + 0] = br.ReadByte();    // Blue
                    data[dstIndex + 3] = br.ReadByte();    // Alpha
                    dstIndex += COMPONENTS;
                }
            }

            return new TextureData
            {
                Width = width,
                Height = height,
                Components = COMPONENTS,
                Data = data
            };
        }

        private static int GetNearestPowerOfTwo(int value)
        {
            return (int)Math.Pow(2, Math.Ceiling(Math.Log(value, 2)));
        }
    }
}