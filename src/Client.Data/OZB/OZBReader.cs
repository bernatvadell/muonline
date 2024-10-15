using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.OZB
{
    public class OZBReader : BaseReader<OZB>
    {
        protected override OZB Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));

            var fileType = br.ReadString(3);

            var version = br.ReadByte();

            if (version != 0 && version != 4)
                throw new FileLoadException($"Invalid OZB version. Expected 0 or 4, Received: {version}");


            return fileType switch
            {
                "BM6" => this.ReadBM6(br, version),
                "BM8" => this.ReadBM8(br, version),
                _ => throw new FileLoadException($"Invalid OZB file type. Expected BM6 or BM8, Received: {fileType}"),
            };
        }

        private OZB ReadBM8(BinaryReader br, byte version)
        {
            // header (14 bytes)
            var type = br.ReadInt16();
            var size = br.ReadInt32();
            var res1 = br.ReadInt16();
            var res2 = br.ReadInt16();
            var offBits = br.ReadInt32();

            // info (40 bytes)
            var biSize = br.ReadInt32();
            var width = br.ReadInt32();
            var height = br.ReadInt32();
            var planes = br.ReadInt16();
            var bitCount = br.ReadInt16();
            var compression = br.ReadInt32();
            var sizeImage = br.ReadInt32();
            var xpelsPerMeter = br.ReadInt32();
            var ypelsPerMeter = br.ReadInt32();
            var clrUsed = br.ReadInt32();
            var clrImportant = br.ReadInt32();

            var bmpHeader = br.ReadBytes(1026);

            var backTerrainHeight = br.ReadBytes(width * height);

            return new OZB
            {
                Version = version,
                Width = width,
                Height = height,
                Data = backTerrainHeight.Select(x => Color.FromArgb(255, x, x, x)).ToArray()
            };
        }

        private OZB ReadBM6(BinaryReader br, byte version)
        {
            // header
            var type = br.ReadInt16();
            var size = br.ReadInt32();
            var res1 = br.ReadInt16();
            var res2 = br.ReadInt16();
            var offBits = br.ReadInt32();

            // info
            var biSize = br.ReadInt32();
            var width = br.ReadInt32();
            var height = br.ReadInt32();
            var planes = br.ReadInt16();
            var bitCount = br.ReadInt16();
            var compression = br.ReadInt32();
            var sizeImage = br.ReadInt32();
            var xpelsPerMeter = br.ReadInt32();
            var ypelsPerMeter = br.ReadInt32();
            var clrUsed = br.ReadInt32();
            var clrImportant = br.ReadInt32();

            Color[] data = new Color[width * height];

            for (var i = 0; i < data.Length; i++)
            {
                var b = br.ReadByte();
                var g = br.ReadByte();
                var r = br.ReadByte();
                data[i] = Color.FromArgb(255, r, g, b);
            }

            return new OZB
            {
                Version = version,
                Width = width,
                Height = height,
                Data = data
            };
        }
    }
}
