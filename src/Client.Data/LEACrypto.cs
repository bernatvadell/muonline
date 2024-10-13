using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Client.Data
{
    public static class LEACrypto
    {
        private static byte[] LEA_KEY = [0xcc, 0x50, 0x45, 0x13, 0xc2, 0xa6, 0x57, 0x4e, 0xd6, 0x9a, 0x45, 0x89, 0xbf, 0x2f, 0xbc, 0xd9, 0x39, 0xb3, 0xb3, 0xbd, 0x50, 0xbd, 0xcc, 0xb6, 0x85, 0x46, 0xd1, 0xd6, 0x16, 0x54, 0xe0, 0x87];

        public static byte[] Decrypt(byte[] source)
        {
            var lea = new LEA.Symmetric.Lea.Ecb();
            lea.Init(LEA.BlockCipher.Mode.DECRYPT, LEA_KEY);
            return lea.DoFinal(source);
        }
    }
}
