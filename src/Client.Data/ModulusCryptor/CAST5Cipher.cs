using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;

namespace Client.Data.ModulusCryptor
{
    public class CAST5Cipher : ICipher
    {
        private readonly IBlockCipher cipher;
        private readonly KeyParameter keyParam;

        public CAST5Cipher(byte[] key)
        {
            
            cipher = new Cast5Engine();
            keyParam = new KeyParameter(key.Take(16).ToArray()); // CAST5 utiliza una clave de hasta 128 bits
        }

        public int GetBlockSize()
        {
            return cipher.GetBlockSize(); // Generalmente 8 bytes para CAST5
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
