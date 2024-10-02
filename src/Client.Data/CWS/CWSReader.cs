using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.CWS
{

    public class CWSReader : BaseReader<CameraWalkScript>
    {
        private const uint MagicNumber = 0x00535743;
        protected override CameraWalkScript Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));

            var sign = br.ReadUInt32();

            if (sign != 0x00535743)
                throw new FileLoadException($"Invalid file type. Expected magic number {MagicNumber}, Received: {sign}.");

            var size = br.ReadInt32();

            var wps = br.ReadStructArray<WayPoint>(size);

            return new CameraWalkScript
            {
                WayPoints = wps
            };
        }
    }
}
