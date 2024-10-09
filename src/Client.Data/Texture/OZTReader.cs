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

        protected override TextureData Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));
            var header = br.ReadBytes(16);
            short nx = br.ReadInt16();
            short ny = br.ReadInt16();
            byte depth = br.ReadByte();
            byte u1 = br.ReadByte();

            if (depth != 32 || nx > MAX_WIDTH || ny > MAX_HEIGHT)
                throw new FileLoadException("Invalid OZT file");

            int width = 0, height = 0;
            for (int i = 1; i <= MAX_WIDTH; i <<= 1)
            {
                width = i;
                if (i >= nx) break;
            }
            for (int i = 1; i <= MAX_HEIGHT; i <<= 1)
            {
                height = i;
                if (i >= ny) break;
            }

            int bufferSize = width * height * 4;
            var data = new byte[bufferSize];

            for (int y = 0; y < ny; y++)
            {
                int dstIndex = (ny - 1 - y) * width * 4;

                for (int x = 0; x < nx; x++)
                {
                    byte b = br.ReadByte(), g = br.ReadByte(), r = br.ReadByte(), a = br.ReadByte();

                    //float alphaFactor = a / 255f;
                    //data[dstIndex + 0] = (byte)(r * alphaFactor);
                    //data[dstIndex + 1] = (byte)(g * alphaFactor);
                    //data[dstIndex + 2] = (byte)(b * alphaFactor);

                    data[dstIndex + 0] = r;
                    data[dstIndex + 1] = g;
                    data[dstIndex + 2] = b;
                    data[dstIndex + 3] = a;

                    dstIndex += 4;
                }
            }

            return new TextureData
            {
                Width = width,
                Height = height,
                Components = 4,
                Data = data
            };
        }
    }
}
