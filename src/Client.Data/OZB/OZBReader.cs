using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.OZB
{
    public class OZBReader : BaseReader<OZB>
    {
        protected override OZB Read(byte[] buffer)
        {
            var expectedSize = 4 + 1080 + 256 * 256;

            if (buffer.Length != expectedSize)
                throw new FileLoadException("Invalid OZB file size");

            using var br = new BinaryReader(new MemoryStream(buffer));

            var fileType = br.ReadString(3);

            if (fileType != "BM8")
                throw new FileLoadException($"Invalid OZB file type. Expected BM8, Received: {fileType}");

            var version = br.ReadByte();

            if (version != 0 && version != 4)
                throw new FileLoadException($"Invalid OZB version. Expected 0 or 4, Received: {version}");

            var bmpHeader = br.ReadBytes(1080);

            var backTerrainHeight = br.ReadBytes(256 * 256);

            return new OZB
            {
                Version = version,
                BackTerrainHeight = backTerrainHeight
            };
        }
    }
}
