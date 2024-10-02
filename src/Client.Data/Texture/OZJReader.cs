using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.Texture
{
    public class OZJReader : BaseReader<TextureData>
    {
        public const int MAX_WIDTH = 1024;
        public const int MAX_HEIGHT = 1024;

        protected override TextureData Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));

            var magicNumber = br.ReadUInt32();

            if (magicNumber != 0xE0FFD8FF && magicNumber != 0x4B6D4BFF)
                throw new FileLoadException("Invalid OZJ file");

            var version = br.ReadInt16();

            if (version != 27956 && version != 4096)
                throw new FileLoadException($"Invalid OZJ version. Expected 4096, Received: {version}");

            var format = br.ReadString(4);

            if (format != "u" && format != "JFIF")
                throw new FileLoadException($"Invalid OZJ format. Expected JFIF, Received: {format}");

            var u1 = br.ReadBytes(3);
            var isTopDownSort = br.ReadBoolean();
            var u2 = br.ReadBytes(10);

            var jpgBuff = br.ReadBytes(buffer.Length - 24);

            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgb24>(jpgBuff);

            int jpegWidth = image.Width;
            int jpegHeight = image.Height;

            if (jpegWidth > MAX_WIDTH || jpegHeight > MAX_HEIGHT)
                throw new FileLoadException("Invalid OZJ Dimensions");

            var data = new byte[jpegWidth * jpegHeight * 3];
            image.CopyPixelDataTo(data);

            if (!isTopDownSort)
            {
                var topDown = new byte[data.Length];

                int rowSize = (int)jpegWidth * 3;

                for (int y = 0; y < jpegHeight; y++)
                    Buffer.BlockCopy(data, y * rowSize, topDown, ((int)jpegHeight - y - 1) * rowSize, rowSize);

                data = topDown;
            }

            return new TextureData
            {
                Width = jpegWidth,
                Height = jpegHeight,
                Components = 3,
                Data = data
            };
        }
    }
}
