using BCnEncoder.Decoder;
using BCnEncoder.Shared;


namespace Client.Data.Texture
{
    public class OZDReader : BaseReader<TextureData>
    {
        private readonly BcDecoder _decoder;

        public class DecoderSettings
        {
            public bool UseParallel { get; set; } = false;
        }

        public OZDReader(DecoderSettings? settings = null)
        {
            _decoder = new BcDecoder();
            if (settings != null)
            {
                _decoder.Options.IsParallel = settings.UseParallel;
            }
        }

        protected override TextureData Read(byte[] buffer)
        {
            buffer = ModulusCryptor.ModulusCryptor.Decrypt(buffer);
            if (buffer[0] == 'D' && buffer[1] == 'D' && buffer[2] == 'S' && buffer[3] == ' ')
                return ReadDDS(buffer);

            throw new ApplicationException($"Invalid OZD file");
        }

        private TextureData ReadDDS(byte[] buffer)
        {
            var header = buffer.AsSpan(0, 128);
            using var br = new BinaryReader(new MemoryStream(header.ToArray()));

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

            CompressionFormat format = pixelFormat switch
            {
                "DXT1" => CompressionFormat.Bc1,
                "DXT3" => CompressionFormat.Bc2,
                "DXT5" => CompressionFormat.Bc3,
                _ => throw new ApplicationException($"Unsupported DDS format: {pixelFormat}")
            };

            var data = buffer.AsSpan(128).ToArray();

            try
            {
                ColorRgba32[] decodedPixels = _decoder.DecodeRaw(data, width, height, format);

                byte[] decompressedData = new byte[decodedPixels.Length * 4];
                for (int i = 0; i < decodedPixels.Length; i++)
                {
                    int byteIndex = i * 4;
                    decompressedData[byteIndex] = decodedPixels[i].r;
                    decompressedData[byteIndex + 1] = decodedPixels[i].g;
                    decompressedData[byteIndex + 2] = decodedPixels[i].b;
                    decompressedData[byteIndex + 3] = decodedPixels[i].a;
                }

                return new TextureData
                {
                    Components = 4,
                    Width = width,
                    Height = height,
                    Data = decompressedData
                };
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to decompress texture: {ex.Message}", ex);
            }
        }
    }
}