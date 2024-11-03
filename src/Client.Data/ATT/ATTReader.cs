namespace Client.Data.ATT
{
    public class ATTReader : BaseReader<TerrainAttribute>
    {
        private static readonly byte[] MASK = { 0xFC, 0xCF, 0xAB };

        protected override TerrainAttribute Read(byte[] buffer)
        {
            if (buffer.Length > 4 &&
                buffer[0] == (byte)'A' &&
                buffer[1] == (byte)'T' &&
                buffer[2] == (byte)'T' &&
                buffer[3] == 1)
            {
                byte[] enc = new byte[buffer.Length - 4];
                Buffer.BlockCopy(buffer, 4, enc, 0, enc.Length);
                buffer = ModulusCryptor.ModulusCryptor.Decrypt(enc);
            }
            else
            {
                buffer = FileCryptor.Decrypt(buffer);
            }

            int bufferLength = buffer.Length;
            int terrainSize = Constants.TERRAIN_SIZE;
            int expectedSize = terrainSize * terrainSize + 4;
            int expectedExtendedSize = terrainSize * terrainSize * 2 + 4;

            if (bufferLength != expectedSize && bufferLength != expectedExtendedSize)
                throw new FileLoadException($"Unexpected file size. Expected: {expectedSize} or for extended ver: {expectedExtendedSize}. Current: {bufferLength}");

            for (int i = 0; i < bufferLength; i++)
            {
                buffer[i] ^= MASK[i % MASK.Length];
            }

            bool isExtended = bufferLength == expectedExtendedSize;

            byte version = buffer[0];
            byte index = buffer[1];
            byte width = buffer[2];
            byte height = buffer[3];

            if (version != 0)
                throw new FileLoadException($"Unknown version. Expected 0 and Received {version}.");

            if (width != 255 || height != 255)
                throw new FileLoadException($"Invalid width or height. Expected 255x255 and Received {width}x{height}.");

            TerrainAttribute model = new TerrainAttribute
            {
                Version = version,
                Index = index,
                Width = width,
                Height = height
            };

            int offset = 4;
            for (int i = 0; i < Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE; i++)
            {
                ushort b;
                if (isExtended)
                {
                    if (offset + 2 > bufferLength)
                        throw new FileLoadException($"Unexpected end of buffer while reading extended TerrainWall at index {i}.");
                    b = BitConverter.ToUInt16(buffer, offset);
                    offset += 2;
                }
                else
                {
                    if (offset + 1 > bufferLength)
                        throw new FileLoadException($"Unexpected end of buffer while reading TerrainWall at index {i}.");
                    b = buffer[offset];
                    offset += 1;
                }

                b &= 0xFF;

                if (b >= 0x80)
                    throw new FileLoadException($"Invalid value at TW index {i}. Expected 0-127 and Received {b}.");

                model.TerrainWall[i] = (TWFlags)b;
            }

            return model;
        }
    }
}
