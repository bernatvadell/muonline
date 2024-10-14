using Org.BouncyCastle.Bcpg.OpenPgp;

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

            IMapObject[] objects = version switch
            {
                1 => br.ReadStructArray<MapObjectV1>(count).OfType<IMapObject>().ToArray(),
                2 => br.ReadStructArray<MapObjectV1>(count).OfType<IMapObject>().ToArray(),
                3 => br.ReadStructArray<MapObjectV3>(count).OfType<IMapObject>().ToArray(),
                _ => throw new NotImplementedException($"Version {version} not implemented"),
            };

            return new OBJ
            {
                Version = version,
                MapNumber = mapNumber,
                Objects = objects
            };
        }
    }
}
