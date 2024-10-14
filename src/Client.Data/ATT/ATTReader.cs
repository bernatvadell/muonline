
using Client.Data.ModulusCryptor;

namespace Client.Data.ATT
{
    public class ATTReader : BaseReader<TerrainAttribute>
    {
        private static byte[] MASK = [0xFC, 0xCF, 0xAB];

        protected override TerrainAttribute Read(byte[] buffer)
        {
            if (buffer.Length > 4 && buffer[0] == 'A' && buffer[1] == 'T' && buffer[2] == 'T' && buffer[3] == 1)
            {
                var enc = new byte[buffer.Length - 4];
                Array.Copy(buffer, 4, enc, 0, enc.Length);
                buffer = ModulusCryptor.ModulusCryptor.Decrypt(enc);
            }
            else
            {
                buffer = FileCryptor.Decrypt(buffer);
            }

            var bufferLength = buffer.Length;
            var expectedSize = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE + 4;
            var expectedExtendedSize = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE * 2 + 4;

            if (bufferLength != expectedSize && bufferLength != expectedExtendedSize)
                throw new FileLoadException($"Unexpected file size. Expected: {expectedSize} or for extended ver: {expectedExtendedSize}. Current: {bufferLength}");

            for (int i = 0; i < buffer.Length; ++i)
                buffer[i] ^= MASK[i % 3];

            var isExtended = buffer.Length == expectedExtendedSize;

            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);

            var model = new TerrainAttribute
            {
                Version = br.ReadByte(),
                Index = br.ReadByte(),
                Width = br.ReadByte(),
                Height = br.ReadByte()
            };

            if (model.Version != 0)
                throw new FileLoadException($"Unknown version. Expected 0 and Received {model.Version}.");

            if (model.Width != 255 || model.Height != 255)
                throw new FileLoadException($"Invalid width or height. Expected 255x255 and Received {model.Width}x{model.Height}.");

            for (int i = 0; i < model.TerrainWall.Length; ++i)
            {
                var b = isExtended ? br.ReadUInt16() : br.ReadByte();
                b = (ushort)(b & 0xFF);

                if (b >= 0x80)
                    throw new FileLoadException($"Invalid value at TW index {i}. Expected 0-127 and Received {b}.");

                model.TerrainWall[i] = (TWFlags)(b);
            }

            return model;
        }
    }
}
