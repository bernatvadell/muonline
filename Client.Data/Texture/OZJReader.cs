using System.Text;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Client.Data.Texture
{
    public class OZJReader : BaseReader<TextureData>
    {
        public const int MAX_WIDTH = 1024;
        public const int MAX_HEIGHT = 1024;

        protected override TextureData Read(byte[] buffer)
        {
            // Carregar o buffer sem necessidade de conversão adicional para array
            var spanBuffer = buffer.AsSpan();

            // Ler informações diretamente do buffer
            uint magicNumber = BitConverter.ToUInt32(spanBuffer.Slice(0, 4));
            short version = BitConverter.ToInt16(spanBuffer.Slice(4, 2));
            string format = Encoding.ASCII.GetString(spanBuffer.Slice(6, 4));

            bool isTopDownSort = BitConverter.ToBoolean(spanBuffer.Slice(17, 1));

            // Extrair dados JPEG a partir do buffer
            var jpgBuffer = spanBuffer.Slice(24);

            using var image = Image.Load<Rgb24>(jpgBuffer);
            int jpegWidth = image.Width;
            int jpegHeight = image.Height;

            if (jpegWidth > MAX_WIDTH || jpegHeight > MAX_HEIGHT)
            {
                throw new FileLoadException($"Invalid OZJ Dimensions: Width={jpegWidth}, Height={jpegHeight}");
            }

            // Copiar dados de imagem
            var data = new byte[jpegWidth * jpegHeight * 3];
            image.CopyPixelDataTo(data);

            // Verificar necessidade de ordenação top-down
            if (!isTopDownSort)
            {
                int rowSize = jpegWidth * 3;
                for (int y = 0; y < jpegHeight; y++)
                {
                    int topIndex = y * rowSize;
                    int bottomIndex = (jpegHeight - y - 1) * rowSize;

                    for (int i = 0; i < rowSize; i++)
                    {
                        byte temp = data[topIndex + i];
                        data[topIndex + i] = data[bottomIndex + i];
                        data[bottomIndex + i] = temp;
                    }
                }
            }

            return new TextureData
            {
                Width = jpegWidth,
                Height = jpegHeight,
                Components = 3,
                Data = data,
                IsCompressed = false,
                Format = TextureSurfaceFormat.Color
            };
        }
    }
}