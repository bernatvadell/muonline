using Client.Data.ModulusCryptor;
using ManagedSquish;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;

namespace Client.Data.Texture
{
    public class OZDReader : BaseReader<TextureData>
    {
        protected override TextureData Read(byte[] buffer)
        {
            buffer = ModulusCryptor.ModulusCryptor.Decrypt(buffer);

            if (buffer[0] == 'D' && buffer[1] == 'D' && buffer[2] == 'S' && buffer[3] == ' ')
                return this.ReadDDS(buffer);

            /*var version = br.ReadByte();
            var size = br.ReadInt32();

            using var uncompressed = new MemoryStream();
            using var dec = new ZLibStream(ms, CompressionMode.Decompress);
            dec.CopyTo(uncompressed);

            var rawData = uncompressed.ToArray();

            if (rawData.Length != size)
                throw new ApplicationException($"Invalid file, expected {size}, received: {rawData.Length}");

            byte[] pixelData = Squish.DecompressImage([], 0, 0, SquishFlags.Dxt1);
            */
            throw new ApplicationException($"Invalid OZD file");
        }

        private TextureData ReadDDS(byte[] buffer)
        {
            var header = new byte[128];
            Array.Copy(buffer, 0, header, 0, 128);

            using var br = new BinaryReader(new MemoryStream(header));
            var signature = br.ReadString(4);
            var headerSize = br.ReadInt32();
            var flags = br.ReadInt32();
            var height = br.ReadInt32();
            var width = br.ReadInt32();
            var pitchOrLinearSize = br.ReadInt32();
            var depth = br.ReadInt32();
            var mipMapCount = br.ReadInt32();
            br.BaseStream.Seek(84, SeekOrigin.Begin);
            var pixelFormat = br.ReadString(4);

            var squishFlags = SquishFlags.Dxt1;

            squishFlags = pixelFormat switch
            {
                "DXT1" => SquishFlags.Dxt1,
                "DXT3" => SquishFlags.Dxt3,
                "DXT5" => SquishFlags.Dxt5,
                _ => throw new ApplicationException($"Invalid pixel format: {pixelFormat}"),
            };

            var data = new byte[buffer.Length - 128];
            Array.Copy(buffer, 128, data, 0, data.Length);

            byte[] decompressedData = Squish.DecompressImage(data, width, height, squishFlags);

            return new TextureData
            {
                Components = 4,
                Width = width,
                Height = height,
                Data = decompressedData
            };
        }
    }
}
