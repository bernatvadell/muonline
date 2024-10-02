namespace Client.Data.OBJS
{
    public class OBJReader : BaseReader<OBJ>
    {
        protected override OBJ Read(byte[] buffer)
        {
            buffer = FileCryptor.Decrypt(buffer);

            using var br = new BinaryReader(new MemoryStream(buffer));

            var version = br.ReadByte();
            var mapNumber = br.ReadByte();
            var count = br.ReadInt16();
            var objects = br.ReadStructArray<MapObject>(count);

            return new OBJ
            {
                Version = version,
                MapNumber = mapNumber,
                Objects = objects
            };
        }
    }
}
