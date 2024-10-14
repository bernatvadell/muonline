using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;

namespace Client.Data.ModulusCryptor
{
    public class RC5Cipher : ICipher
    {
        private readonly IBlockCipher cipher;
        private readonly KeyParameter keyParam;

        public RC5Cipher(byte[] key)
        {
            cipher = new RC532Engine();
            keyParam = new RC5Parameters(key[..16], 16);
        }

        public int GetBlockSize()
        {
            return cipher.GetBlockSize();
        }

        public void BlockDecrypt(byte[] inBuf, int len, byte[] outBuf)
        {
            int blockSize = cipher.GetBlockSize();
            for (int i = 0; i < len; i += blockSize)
            {
                cipher.ProcessBlock(inBuf, i, outBuf, i);
            }
        }

        public void Init()
        {
            cipher.Init(false, keyParam);
        }
    }

}
