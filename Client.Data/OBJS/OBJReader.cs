namespace Client.Data.OBJS
{
    public class OBJReader : BaseReader<OBJ>
    {
        protected override OBJ Read(byte[] buffer)
        {
            try
            {
                buffer = FileCryptor.Decrypt(buffer);

                using var memoryStream = new MemoryStream(buffer);
                using var br = new BinaryReader(memoryStream);

                var version = br.ReadByte();
                var mapNumber = br.ReadByte();
                var count = br.ReadInt16();

                IMapObject[] objects = version switch
                {
                    0 => br.ReadStructArray<MapObjectV0>(count).Cast<IMapObject>().ToArray(),
                    1 => br.ReadStructArray<MapObjectV1>(count).Cast<IMapObject>().ToArray(),
                    2 => br.ReadStructArray<MapObjectV2>(count).Cast<IMapObject>().ToArray(),
                    3 => br.ReadStructArray<MapObjectV3>(count).Cast<IMapObject>().ToArray(),
                    _ => throw new NotImplementedException($"Version {version} not implemented"),
                };

                return new OBJ
                {
                    Version = version,
                    MapNumber = mapNumber,
                    Objects = objects
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to read OBJ data.", ex);
            }
        }
    }
}
