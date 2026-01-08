using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Data
{
    public class BuxCryptor
    {
        private static readonly byte[] s_buxCode = { 0xfc, 0xcf, 0xab };

        public static byte[] Convert(byte[] buffer)
        {
            // Same behavior as original: XOR with repeating key, starting at index 0 for each converted block.
            var dst = new byte[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                dst[i] = (byte)(buffer[i] ^ s_buxCode[i % s_buxCode.Length]);
            }
            return dst;
        }

        public static void ConvertPerRecordInPlace(Span<byte> data, int recordSize)
        {
            for (int i = 0; i < data.Length; i++)
            {
                int j = i % recordSize;
                data[i] = (byte)(data[i] ^ s_buxCode[j % s_buxCode.Length]);
            }
        }
    }
}
