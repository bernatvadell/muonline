namespace Client.Data.Texture
{
    public class OZDReader : BaseReader<TextureData>
    {
        public class DecoderSettings
        {
            public bool UseParallel { get; set; } = false;
        }

        public OZDReader() { }

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

            TextureSurfaceFormat surfaceFormat = pixelFormat switch
            {
                "DXT1" => TextureSurfaceFormat.Dxt1,
                "DXT3" => TextureSurfaceFormat.Dxt3,
                "DXT5" => TextureSurfaceFormat.Dxt5,
                _ => throw new ApplicationException($"Unsupported DDS format: {pixelFormat}")
            };

            var compressedData = buffer.AsSpan(128).ToArray();

            return new TextureData
            {
                Width = width,
                Height = height,
                Format = surfaceFormat,
                IsCompressed = true,
                Data = compressedData
            };
        }
    }
}