using Client.Data.Texture;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client.Editor
{
    public partial class CTextureEditor : UserControl
    {
        public CTextureEditor()
        {
            InitializeComponent();
        }

        public async void Init(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            switch (ext)
            {
                case ".ozj":
                    {
                        var reader = new OZJReader();
                        var data = await reader.Load(filePath);
                        SetData(data);
                    }
                    break;
                case ".ozt":
                    {
                        var reader = new OZTReader();
                        var data = await reader.Load(filePath);
                        SetData(data);
                    }
                    break;
                default:
                    throw new NotImplementedException($"Extension {ext} not supported");
            }
        }

        public void SetData(TextureData textureData)
        {
            Bitmap bitmap = new Bitmap((int)textureData.Width, (int)textureData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int y = 0; y < textureData.Height; y++)
            {
                for (int x = 0; x < textureData.Width; x++)
                {
                    // Calcular el índice en el array de bytes
                    int index = (y * (int)textureData.Width + x) * textureData.Components;

                    // Extraer los componentes RGB o RGBA
                    byte r = textureData.Data[index];
                    byte g = textureData.Data[index + 1];
                    byte b = textureData.Data[index + 2];
                    byte a = (textureData.Components == 4) ? textureData.Data[index + 3] : (byte)255; // Si son RGB, se asume A = 255

                    // Crear un Color con los componentes
                    Color color = Color.FromArgb(a, r, g, b);

                    bitmap.SetPixel(x, y, color);
                }
            }

            pictureBox1.Image = bitmap;
        }
    }
}
