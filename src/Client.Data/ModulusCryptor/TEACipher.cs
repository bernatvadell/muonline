using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Engines;

namespace Client.Data.ModulusCryptor
{
    public class TEACipher : ICipher
    {
        private readonly IBlockCipher cipher;
        private readonly KeyParameter keyParam;

        public TEACipher(byte[] key)
        {
            cipher = new TeaEngine();
            keyParam = new KeyParameter(key[..16]);
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
