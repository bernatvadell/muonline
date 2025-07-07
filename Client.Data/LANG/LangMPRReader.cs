using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LEA.Symmetric.Lea;

namespace Client.Data.LANG
{
    public class LangMPRReader : BaseReader<ZipFile>
    {
        protected override ZipFile Read(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));
            var zFileBytes = br.ReadBytes(buffer.Length - 4);
            var crc = br.ReadUInt32();
            var calculatedCrc = CalculateCRC(zFileBytes, zFileBytes.Length, 0x12dc);
            
            if (crc != calculatedCrc)
            {
                throw new Exception($"CRC Doesnt match, got {calculatedCrc} expected {crc}");
            }            
            zFileBytes = Decrypt(zFileBytes);
            ZipFile zFile = new(new MemoryStream(zFileBytes));
            zFile.Password = "3q*#P<8ALZy*soC2&eHwrA^@=";
            zFile.UseZip64 = UseZip64.Off;
            return zFile;
        }

        private uint CalculateCRC(byte[] data, int Length, ushort wkey)
        {
            var CRC = (uint)(wkey << 9);

            for (int i = 0; i <= Length - 4; i += 4)
            {
                var temp = BitConverter.ToUInt32(data.AsSpan(i, 4));
                if ((wkey + (i >> 2)) % 2 == 1) CRC += temp;
                else CRC ^= temp;

                if (i % 16 == 0)
                {
                    CRC ^= (wkey + CRC) >> ((i >> 2) % 8) + 1;
                }
            }
            return CRC;
        }

        private byte[] Decrypt(byte[] data)
        {
            byte[] buf = (byte[])data.Clone();
            byte[] XOR_3_KEY = { 0xFC, 0xCF, 0xAB };
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] ^= XOR_3_KEY[i % 3];
                buf[i] ^= 0xDC;
            }
            return buf;
        }
    }
}
