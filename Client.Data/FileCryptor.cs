namespace Client.Data
{
    public static class FileCryptor
    {
        private static byte[] MAP_XOR_KEY = new byte[16]
        {
            0xD1, 0x73, 0x52, 0xF6, 0xD2, 0x9A, 0xCB, 0x27,
            0x3E, 0xAF, 0x59, 0x31, 0x37, 0xB3, 0xE7, 0xA2
        };

        public static byte[] Decrypt(byte[] src)
        {
            var dst = new byte[src.Length];
            ushort mapKey = 0x5E;
            for (int i = 0; i < src.Length; ++i)
            {
                dst[i] = (byte)((src[i] ^ MAP_XOR_KEY[i % 16]) - mapKey);
                mapKey = (ushort)(src[i] + 0x3D & 0xFF);
            }
            return dst;
        }

    }
}
