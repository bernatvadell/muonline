using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;

namespace Client.Data.ModulusCryptor
{
    public class ThreeWayCipher : ICipher
    {
        private const int BLOCK_SIZE = 12; // 96 bits
        private const int NUM_ROUNDS = 11;
        private const uint START_E = 0x0b0b; // Constante de inicio para cifrado
        private const uint START_D = 0xb1b1; // Constante de inicio para descifrado

        private uint[] m_k = new uint[3]; // Clave
        private bool isEncryption; // Indica si es cifrado o descifrado

        public ThreeWayCipher(byte[] key, bool isEncryption = false)
        {
            this.isEncryption = isEncryption;

            // Convertir la clave de bytes a uint32
            for (int i = 0; i < 3; i++)
            {
                m_k[i] = BitConverter.ToUInt32(key, i * 4);
            }

            if (!isEncryption)
            {
                // Transformaciones adicionales para descifrado
                Theta(ref m_k[0], ref m_k[1], ref m_k[2]);
                Mu(ref m_k[0], ref m_k[1], ref m_k[2]);
                m_k[0] = ReverseBytes(m_k[0]);
                m_k[1] = ReverseBytes(m_k[1]);
                m_k[2] = ReverseBytes(m_k[2]);
            }
        }

        public int GetBlockSize()
        {
            return BLOCK_SIZE;
        }

        public void BlockDecrypt(byte[] inBuf, int len, byte[] outBuf)
        {
            if (len % BLOCK_SIZE != 0)
                throw new ArgumentException("La longitud de los datos debe ser múltiplo del tamaño del bloque.");

            for (int i = 0; i < len; i += BLOCK_SIZE)
            {
                byte[] block = new byte[BLOCK_SIZE];
                Array.Copy(inBuf, i, block, 0, BLOCK_SIZE);
                byte[] decryptedBlock = new byte[BLOCK_SIZE];
                DecryptBlock(block, decryptedBlock);
                Array.Copy(decryptedBlock, 0, outBuf, i, BLOCK_SIZE);
            }
        }

        public static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }

        public void DecryptBlock(byte[] input, byte[] output)
        {
            // Convertir el bloque de entrada a uint32 (Little Endian para el descifrado)
            uint a0 = BitConverter.ToUInt32(input, 0);
            uint a1 = BitConverter.ToUInt32(input, 4);
            uint a2 = BitConverter.ToUInt32(input, 8);

            // Aplicar transformación mu
            Mu(ref a0, ref a1, ref a2);

            uint rc = START_D;

            for (int i = 0; i < NUM_ROUNDS; i++)
            {
                a0 ^= m_k[0] ^ (rc << 16);
                a1 ^= m_k[1];
                a2 ^= m_k[2] ^ rc;

                Rho(ref a0, ref a1, ref a2);

                rc <<= 1;
                if ((rc & 0x10000) != 0)
                    rc ^= 0x11011;
            }

            a0 ^= m_k[0] ^ (rc << 16);
            a1 ^= m_k[1];
            a2 ^= m_k[2] ^ rc;

            Theta(ref a0, ref a1, ref a2);
            Mu(ref a0, ref a1, ref a2);

            // Convertir los uint32 a bytes y almacenar en el bloque de salida
            Buffer.BlockCopy(BitConverter.GetBytes(a0), 0, output, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(a1), 0, output, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(a2), 0, output, 8, 4);
        }

        public static void Rho(ref uint a0, ref uint a1, ref uint a2)
        {
            Theta(ref a0, ref a1, ref a2);
            PiGammaPi(ref a0, ref a1, ref a2);
        }

        public static void Theta(ref uint a0, ref uint a1, ref uint a2)
        {
            uint c = a0 ^ a1 ^ a2;
            c = Rotl(c, 16) ^ Rotl(c, 8);

            uint b0 = (a0 << 24) ^ (a2 >> 8) ^ (a1 << 8) ^ (a0 >> 24);
            uint b1 = (a1 << 24) ^ (a0 >> 8) ^ (a2 << 8) ^ (a1 >> 24);

            a0 ^= c ^ b0;
            a1 ^= c ^ b1;
            a2 ^= c ^ (b0 >> 16) ^ (b1 << 16);
        }

        public static void PiGammaPi(ref uint a0, ref uint a1, ref uint a2)
        {
            uint b0 = Rotl(a0, 22);
            uint b2 = Rotl(a2, 1);
            a0 = Rotl((b0 ^ (a1 | (~b2))), 1);
            a2 = Rotl((b2 ^ (b0 | (~a1))), 22);
            a1 ^= (b2 | (~b0));
        }

        public static void Mu(ref uint a0, ref uint a1, ref uint a2)
        {
            a1 = ReverseBits(a1);
            uint t = ReverseBits(a0);
            a0 = ReverseBits(a2);
            a2 = t;
        }

        public static uint ReverseBits(uint value)
        {
            value = ((value & 0xAAAAAAAA) >> 1) | ((value & 0x55555555) << 1);
            value = ((value & 0xCCCCCCCC) >> 2) | ((value & 0x33333333) << 2);
            value = ((value & 0xF0F0F0F0) >> 4) | ((value & 0x0F0F0F0F) << 4);
            value = ((value & 0xFF00FF00) >> 8) | ((value & 0x00FF00FF) << 8);
            value = (value >> 16) | (value << 16);
            return value;
        }

        public static uint Rotl(uint value, int bits)
        {
            return (value << bits) | (value >> (32 - bits));
        }

        public static uint Rotr(uint value, int bits)
        {
            return (value >> bits) | (value << (32 - bits));
        }

        public void Init()
        {
        }
    }
}
