using System.Text;

namespace Client.Data.ModulusCryptor
{
    public static class ModulusCryptor
    {
        public static byte[] Decrypt(byte[] source)
        {
            if (source.Length < 34)
                throw new ArgumentException("The source buffer is to short");

            byte[] buf = (byte[])source.Clone();

            byte[] key_1 = Encoding.ASCII.GetBytes("webzen#@!01webzen#@!01webzen#@!0");
            byte[] key_2 = new byte[32];

            int algorithm_1 = buf[1];
            int algorithm_2 = buf[0];

            int size = buf.Length;
            uint data_size = (uint)(size - 34);

            ICipher cipher = InitModulusCrypto(algorithm_1, key_1);

            uint block_size = (uint)(1024 - (1024 % cipher.GetBlockSize()));
            cipher.Init();
            
            if (data_size > (4 * block_size))
            {
                uint index = 2 + (data_size >> 1);
                byte[] block = new byte[block_size];
                Array.Copy(buf, index, block, 0, block_size);
                cipher.BlockDecrypt(block, block.Length, block);
                Array.Copy(block, 0, buf, index, block_size);
            }

            if (data_size > block_size)
            {
                uint index = (uint)(size - block_size);
                byte[] block = new byte[block_size];
                Array.Copy(buf, index, block, 0, block_size);
                cipher.BlockDecrypt(block, block.Length, block);
                Array.Copy(block, 0, buf, index, block_size);
                index = 2;
                block = new byte[block_size];
                Array.Copy(buf, index, block, 0, block_size);
                cipher.BlockDecrypt(block, block.Length, block);
                Array.Copy(block, 0, buf, index, block_size);
            }

            // Copiar key_2 desde buf[2] (32 bytes)
            Array.Copy(buf, 2, key_2, 0, 32);

            cipher = InitModulusCrypto(algorithm_2, key_2);
            cipher.Init();
            block_size = (uint)(data_size - (data_size % cipher.GetBlockSize()));

            if (block_size > 0)
            {
                byte[] finalBlock = new byte[block_size];
                Array.Copy(buf, 34, finalBlock, 0, block_size);
                cipher.BlockDecrypt(finalBlock, finalBlock.Length, finalBlock);
                Array.Copy(finalBlock, 0, buf, 34, block_size);
            }

            // Crear un nuevo arreglo de bytes sin los primeros 34 bytes
            byte[] result = new byte[buf.Length - 34];
            Array.Copy(buf, 34, result, 0, result.Length);

            return result;
        }

        private static ICipher InitModulusCrypto(int algorithm, byte[] key)
        {
            return (algorithm & 7) switch
            {
                0 => new TEACipher(key),
                1 => new ThreeWayCipher(key),
                2 => new CAST5Cipher(key),
                3 => new RC5Cipher(key),
                4 => new RC6Cipher(key),
                5 => new MARSCipher(key),
                6 => new IDEACipher(key),
                7 => new GOSTCipher(key),
                _ => throw new Exception("Unknown algorithm"),
            };
        }
    }
}
