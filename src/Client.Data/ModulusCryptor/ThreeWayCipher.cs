using System;

namespace Client.Data.ModulusCryptor
{
    public class ThreeWayCipher : ICipher
    {
        private uint[] m_k = new uint[3];
        private int m_rounds = 11;
        private const uint START_E = 0x0b0b;
        private const uint START_D = 0xb1b1;
        private const int BLOCK_SIZE = 12;

        public ThreeWayCipher(byte[] key)
        {
            for (int i = 0; i < 3; i++)
            {
                m_k[i] = ((uint)key[4 * i + 3]) |
                         ((uint)key[4 * i + 2] << 8) |
                         ((uint)key[4 * i + 1] << 16) |
                         ((uint)key[4 * i] << 24);
            }

            // Apply key transformations for decryption
            Theta(ref m_k[0], ref m_k[1], ref m_k[2]);
            Mu(ref m_k[0], ref m_k[1], ref m_k[2]);
            m_k[0] = ReverseBytes(m_k[0]);
            m_k[1] = ReverseBytes(m_k[1]);
            m_k[2] = ReverseBytes(m_k[2]);
        }

        private static uint ReverseBytes(uint x)
        {
            return ((x & 0x000000FFU) << 24) |
                   ((x & 0x0000FF00U) << 8) |
                   ((x & 0x00FF0000U) >> 8) |
                   ((x & 0xFF000000U) >> 24);
        }

        private static uint ReverseBits(uint a)
        {
            a = ((a & 0xAAAAAAAAU) >> 1) | ((a & 0x55555555U) << 1);
            a = ((a & 0xCCCCCCCCU) >> 2) | ((a & 0x33333333U) << 2);
            return ((a & 0xF0F0F0F0U) >> 4) | ((a & 0x0F0F0F0FU) << 4);
        }

        private static uint RotateLeft(uint x, int n)
        {
            return (x << n) | (x >> (32 - n));
        }

        private static uint RotlConstant(uint x, int R)
        {
            return RotateLeft(x, R);
        }

        private static void Theta(ref uint a0, ref uint a1, ref uint a2)
        {
            uint c = a0 ^ a1 ^ a2;
            c = RotlConstant(c, 16) ^ RotlConstant(c, 8);
            uint b0 = (a0 << 24) ^ (a2 >> 8) ^ (a1 << 8) ^ (a0 >> 24);
            uint b1 = (a1 << 24) ^ (a0 >> 8) ^ (a2 << 8) ^ (a1 >> 24);
            a0 ^= c ^ b0;
            a1 ^= c ^ b1;
            a2 ^= c ^ (b0 >> 16) ^ (b1 << 16);
        }

        private static void Mu(ref uint a0, ref uint a1, ref uint a2)
        {
            a1 = ReverseBits(a1);
            uint t = ReverseBits(a0);
            a0 = ReverseBits(a2);
            a2 = t;
        }

        private static void PiGammaPi(ref uint a0, ref uint a1, ref uint a2)
        {
            uint b0, b2;
            b2 = RotlConstant(a2, 1);
            b0 = RotlConstant(a0, 22);
            a0 = RotlConstant(b0 ^ (a1 | (~b2)), 1);
            a2 = RotlConstant(b2 ^ (b0 | (~a1)), 22);
            a1 ^= (b2 | (~b0));
        }

        private static void Rho(ref uint a0, ref uint a1, ref uint a2)
        {
            Theta(ref a0, ref a1, ref a2);
            PiGammaPi(ref a0, ref a1, ref a2);
        }

        private static uint ReadUInt32LittleEndian(byte[] buffer, int offset)
        {
            return BitConverter.ToUInt32(buffer, offset);
        }

        private static void WriteUInt32LittleEndian(byte[] buffer, int offset, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, 4);
        }

        public void DecryptBlock(byte[] inBlock, byte[] outBlock)
        {
            if (inBlock.Length != BLOCK_SIZE || outBlock.Length != BLOCK_SIZE)
                throw new ArgumentException("Block size must be 12 bytes.");

            uint a0 = ReadUInt32LittleEndian(inBlock, 0);
            uint a1 = ReadUInt32LittleEndian(inBlock, 4);
            uint a2 = ReadUInt32LittleEndian(inBlock, 8);

            uint rc = START_D;

            Mu(ref a0, ref a1, ref a2);

            for (int i = 0; i < m_rounds; i++)
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

            WriteUInt32LittleEndian(outBlock, 0, a0);
            WriteUInt32LittleEndian(outBlock, 4, a1);
            WriteUInt32LittleEndian(outBlock, 8, a2);
        }

        public void BlockDecrypt(byte[] inBuf, int len, byte[] outBuf)
        {
            if (inBuf == null || outBuf == null || len == 0)
                throw new ArgumentException("Invalid input or output buffer.");

            if (len % BLOCK_SIZE != 0)
                throw new ArgumentException("Length must be multiple of block size.");

            for (int i = 0; i < len; i += BLOCK_SIZE)
            {
                DecryptBlock(inBuf, i, outBuf, i);
            }
        }

        private void DecryptBlock(byte[] inBuf, int inOffset, byte[] outBuf, int outOffset)
        {
            uint a0 = ReadUInt32LittleEndian(inBuf, inOffset);
            uint a1 = ReadUInt32LittleEndian(inBuf, inOffset + 4);
            uint a2 = ReadUInt32LittleEndian(inBuf, inOffset + 8);

            uint rc = START_D;

            Mu(ref a0, ref a1, ref a2);

            for (int i = 0; i < m_rounds; i++)
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

            WriteUInt32LittleEndian(outBuf, outOffset, a0);
            WriteUInt32LittleEndian(outBuf, outOffset + 4, a1);
            WriteUInt32LittleEndian(outBuf, outOffset + 8, a2);
        }

        public int GetBlockSize()
        {
            return BLOCK_SIZE;
        }

        public void Init()
        {
            // No initialization required for this implementation
        }
    }
}
