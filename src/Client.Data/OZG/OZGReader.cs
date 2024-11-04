using System.IO.Compression;

namespace Client.Data.Texture
{
    public class OZGReader : BaseReader<byte[]>
    {
        protected override byte[] Read(byte[] buffer)
        {
            buffer = ModulusCryptor.ModulusCryptor.Decrypt(buffer);

            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);

            var type = br.ReadString(3);
            var version = br.ReadByte();
            var size = br.ReadInt32();

            using var uncompressed = new MemoryStream();
            using var dec = new ZLibStream(ms, CompressionMode.Decompress);
            dec.CopyTo(uncompressed);

            var rawData = uncompressed.ToArray();

            return rawData;
        }
    }
}
