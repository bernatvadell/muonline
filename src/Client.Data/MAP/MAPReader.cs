using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.MAP
{
    public class MapReader : BaseReader<TerrainMapping>
    {
        protected override TerrainMapping Read(byte[] buffer)
        {
            buffer = FileCryptor.Decrypt(buffer);

            using var br = new BinaryReader(new MemoryStream(buffer));

            var terrainMapping = br.ReadStruct<TerrainMapping>();

            return terrainMapping;
        }
    }
}
