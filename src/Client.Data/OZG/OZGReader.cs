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
    public class OZGReader : BaseReader<byte[]>
    {
        protected override byte[] Read(byte[] buffer)
        {
            buffer = ModulusCryptor.ModulusCryptor.Decrypt(buffer);
            File.WriteAllBytes("C:\\Users\\dito1\\Downloads\\test1.swf", buffer);

            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);


            var type = br.ReadString(3);
            var version = br.ReadByte();
            var size = br.ReadInt32();

            using var uncompressed = new MemoryStream();
            using var dec = new ZLibStream(ms, CompressionMode.Decompress);
            dec.CopyTo(uncompressed);

            var rawData = uncompressed.ToArray();

            File.WriteAllBytes("C:\\Users\\dito1\\Downloads\\testdec.swf", rawData);

            return rawData;
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

            // Crear una imagen de ImageSharp
            using (Image<Rgba32> image = new Image<Rgba32>(width, height))
            {
                // Convertir los datos descomprimidos en píxeles para ImageSharp
                int bytesPerPixel = 4; // RGBA = 4 bytes por píxel
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * bytesPerPixel;
                        byte r = decompressedData[index];
                        byte g = decompressedData[index + 1];
                        byte b = decompressedData[index + 2];
                        byte a = decompressedData[index + 3];

                        // Asignar el color al píxel en la imagen
                        image[x, y] = new Rgba32(r, g, b, a);
                    }
                }

                // Guardar la imagen como PNG
                image.Save("C:\\Users\\dito1\\Downloads\\test.png");
            }

            return null;
        }
    }
}
