namespace Client.Data.MAP
{
    public class MapReader : BaseReader<TerrainMapping>
    {
        protected override TerrainMapping Read(byte[] buffer)
        {
            if (buffer.Length > 4 && buffer[0] == 'M' && buffer[1] == 'A' && buffer[2] == 'P' && buffer[3] == 1)
            {
                var enc = new byte[buffer.Length - 4];
                Array.Copy(buffer, 4, enc, 0, enc.Length);
                buffer = ModulusCryptor.ModulusCryptor.Decrypt(enc);
            }
            else
            {
                buffer = FileCryptor.Decrypt(buffer);
            }

            using var br = new BinaryReader(new MemoryStream(buffer));

            var terrainMapping = br.ReadStruct<TerrainMapping>();

            return terrainMapping;
        }
    }
}
