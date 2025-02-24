using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;

namespace Client.Data.ModulusCryptor
{
    public class RC6Cipher : ICipher
    {
        private readonly IBlockCipher cipher;
        private readonly KeyParameter keyParam;
        
        public RC6Cipher(byte[] key)
        {
            cipher = new RC6Engine();
            keyParam = new KeyParameter(key[..16]);
        }

        public int GetBlockSize()
        {
            return cipher.GetBlockSize();
        }

        public void Init()
        {
            cipher.Init(false, keyParam);
        }

        public void BlockDecrypt(byte[] inBuf, int len, byte[] outBuf)
        {
            int blockSize = cipher.GetBlockSize();
            for (int i = 0; i < len; i += blockSize)
            {
                cipher.ProcessBlock(inBuf, i, outBuf, i);
            }
        }
    }

}
