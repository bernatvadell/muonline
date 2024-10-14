using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;

namespace Client.Data.ModulusCryptor
{
    public class GOSTCipher : ICipher
    {
        private readonly IBlockCipher cipher;
        private readonly KeyParameter keyParam;

        public GOSTCipher(byte[] key)
        {
            cipher = new Gost28147Engine();
            keyParam = new KeyParameter(key.Take(32).ToArray()); // GOST utiliza una clave de 256 bits
        }

        public int GetBlockSize()
        {
            return cipher.GetBlockSize(); // Generalmente 8 bytes para GOST
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
