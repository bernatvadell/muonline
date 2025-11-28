using System;
using Microsoft.Xna.Framework;

namespace Client.Main.Content
{
    /// <summary>
    /// Software DXT (S3TC) texture decompression for platforms that don't support it natively (e.g., Android).
    /// Supports DXT1, DXT3, and DXT5 formats.
    /// </summary>
    public static class DxtDecoder
    {
        /// <summary>
        /// Decompresses DXT1 compressed texture data to RGBA8888 format.
        /// </summary>
        public static byte[] DecompressDXT1(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            int offset = 0;

            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    DecompressBlockDXT1(x, y, data, offset, rgba, width);
                    offset += 8; // DXT1 block is 8 bytes
                }
            }
            return rgba;
        }

        /// <summary>
        /// Decompresses DXT3 compressed texture data to RGBA8888 format.
        /// </summary>
        public static byte[] DecompressDXT3(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            int offset = 0;

            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    DecompressBlockDXT3(x, y, data, offset, rgba, width);
                    offset += 16; // DXT3 block is 16 bytes (8 alpha + 8 color)
                }
            }
            return rgba;
        }

        /// <summary>
        /// Decompresses DXT5 compressed texture data to RGBA8888 format.
        /// </summary>
        public static byte[] DecompressDXT5(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            int offset = 0;

            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    DecompressBlockDXT5(x, y, data, offset, rgba, width);
                    offset += 16; // DXT5 block is 16 bytes (8 alpha + 8 color)
                }
            }
            return rgba;
        }

        private static void DecompressBlockDXT1(int x, int y, byte[] data, int offset, byte[] rgba, int width)
        {
            ushort c0 = BitConverter.ToUInt16(data, offset);
            ushort c1 = BitConverter.ToUInt16(data, offset + 2);
            uint lookup = BitConverter.ToUInt32(data, offset + 4);

            Color[] colors = new Color[4];
            colors[0] = Unpack565(c0);
            colors[1] = Unpack565(c1);

            if (c0 > c1)
            {
                colors[2] = Lerp(colors[0], colors[1], 1f / 3f);
                colors[3] = Lerp(colors[0], colors[1], 2f / 3f);
            }
            else
            {
                colors[2] = Lerp(colors[0], colors[1], 0.5f);
                colors[3] = new Color(0, 0, 0, 0); // Transparent
            }

            for (int by = 0; by < 4; by++)
            {
                for (int bx = 0; bx < 4; bx++)
                {
                    int code = (int)(lookup & 3);
                    lookup >>= 2;

                    Color final = colors[code];
                    SetPixel(rgba, x + bx, y + by, width, final);
                }
            }
        }

        private static void DecompressBlockDXT3(int x, int y, byte[] data, int offset, byte[] rgba, int width)
        {
            // Alpha block (8 bytes) - 4-bit alpha per pixel
            ulong alphaData = BitConverter.ToUInt64(data, offset);

            // Color block starts after 8 bytes of alpha
            int colorOffset = offset + 8;
            ushort c0 = BitConverter.ToUInt16(data, colorOffset);
            ushort c1 = BitConverter.ToUInt16(data, colorOffset + 2);
            uint lookup = BitConverter.ToUInt32(data, colorOffset + 4);

            Color[] colors = new Color[4];
            colors[0] = Unpack565(c0);
            colors[1] = Unpack565(c1);
            colors[2] = Lerp(colors[0], colors[1], 1f / 3f);
            colors[3] = Lerp(colors[0], colors[1], 2f / 3f);

            for (int by = 0; by < 4; by++)
            {
                for (int bx = 0; bx < 4; bx++)
                {
                    int code = (int)(lookup & 3);
                    lookup >>= 2;

                    // Extract 4-bit alpha
                    int alphaIndex = (by * 4) + bx;
                    int alpha4 = (int)((alphaData >> (alphaIndex * 4)) & 0xF);
                    int alpha8 = (alpha4 << 4) | alpha4; // Expand to 8-bit

                    Color final = colors[code];
                    final.A = (byte)alpha8;

                    SetPixel(rgba, x + bx, y + by, width, final);
                }
            }
        }

        private static void DecompressBlockDXT5(int x, int y, byte[] data, int offset, byte[] rgba, int width)
        {
            // Alpha block (8 bytes) - interpolated alpha
            byte a0 = data[offset];
            byte a1 = data[offset + 1];
            ulong alphaLookup = BitConverter.ToUInt64(data, offset) >> 16; // Skip a0, a1

            float[] alphas = new float[8];
            alphas[0] = a0;
            alphas[1] = a1;

            if (a0 > a1)
            {
                for (int i = 2; i < 8; i++)
                    alphas[i] = ((8 - i) * a0 + (i - 1) * a1) / 7f;
            }
            else
            {
                for (int i = 2; i < 6; i++)
                    alphas[i] = ((6 - i) * a0 + (i - 1) * a1) / 5f;
                alphas[6] = 0;
                alphas[7] = 255;
            }

            // Color block starts after 8 bytes of alpha
            int colorOffset = offset + 8;
            ushort c0 = BitConverter.ToUInt16(data, colorOffset);
            ushort c1 = BitConverter.ToUInt16(data, colorOffset + 2);
            uint colorLookup = BitConverter.ToUInt32(data, colorOffset + 4);

            Color[] colors = new Color[4];
            colors[0] = Unpack565(c0);
            colors[1] = Unpack565(c1);
            colors[2] = Lerp(colors[0], colors[1], 1f / 3f);
            colors[3] = Lerp(colors[0], colors[1], 2f / 3f);

            for (int by = 0; by < 4; by++)
            {
                for (int bx = 0; bx < 4; bx++)
                {
                    int alphaCode = (int)(alphaLookup & 7);
                    alphaLookup >>= 3;

                    int colorCode = (int)(colorLookup & 3);
                    colorLookup >>= 2;

                    Color final = colors[colorCode];
                    final.A = (byte)alphas[alphaCode];

                    SetPixel(rgba, x + bx, y + by, width, final);
                }
            }
        }

        /// <summary>
        /// Unpacks RGB565 format to Color (RGBA8888).
        /// </summary>
        private static Color Unpack565(ushort c)
        {
            return new Color(
                (c & 0xF800) >> 8,  // Red: bits 11-15
                (c & 0x07E0) >> 3,  // Green: bits 5-10
                (c & 0x001F) << 3   // Blue: bits 0-4
            );
        }

        /// <summary>
        /// Linear interpolation between two colors.
        /// </summary>
        private static Color Lerp(Color c1, Color c2, float amount)
        {
            float inv = 1f - amount;
            return new Color(
                (int)(c1.R * inv + c2.R * amount),
                (int)(c1.G * inv + c2.G * amount),
                (int)(c1.B * inv + c2.B * amount)
            );
        }

        /// <summary>
        /// Sets a pixel in the RGBA output buffer.
        /// </summary>
        private static void SetPixel(byte[] rgba, int x, int y, int width, Color c)
        {
            // DXT works on 4x4 blocks, so textures might be padded
            // We assume width/height passed here match the texture dimensions
            int idx = (y * width + x) * 4;
            if (idx + 3 < rgba.Length)
            {
                rgba[idx] = c.R;
                rgba[idx + 1] = c.G;
                rgba[idx + 2] = c.B;
                rgba[idx + 3] = c.A;
            }
        }
    }
}
