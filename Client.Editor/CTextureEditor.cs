using Client.Data.OZB;
using Client.Data.Texture;
using System.ComponentModel;

namespace Client.Editor
{
    public partial class CTextureEditor : UserControl
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextureData Data { get; private set; }

        public CTextureEditor()
        {
            InitializeComponent();
            pictureBox1.BackColor = Color.White;
        }

        public async void Init(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            switch (ext)
            {
                case ".ozj":
                    {
                        var reader = new OZJReader();
                        Data = await reader.Load(filePath);
                        SetData();
                    }
                    break;
                case ".ozt":
                    {
                        var reader = new OZTReader();
                        Data = await reader.Load(filePath);
                        SetData();
                    }
                    break;
                case ".ozb":
                    {
                        var reader = new OZBReader();
                        var texture = await reader.Load(filePath);
                        Data = new TextureData
                        {
                            Components = 4,
                            Width = texture.Width,
                            Height = texture.Height,
                            Data = texture.Data.SelectMany(x => new byte[] { x.R, x.G, x.B, x.A }).ToArray()
                        };
                    }
                    break;
                case ".ozd":
                    {
                        var reader = new OZDReader();
                        Data = await reader.Load(filePath);
                        SetData();
                    }
                    break;
                case ".ozp":
                    {
                        var reader = new OZPReader();
                        Data = await reader.Load(filePath);
                        SetData();
                    }
                    break;
                default:
                    throw new NotImplementedException($"Extension {ext} not supported");
            }
        }

        public void SetData()
        {
            var textureData = Data;

            var bitmap = new Bitmap((int)textureData.Width, (int)textureData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var data = textureData.Data;
            var components = textureData.Components;

            switch (textureData.Format)
            {
                case TextureSurfaceFormat.Color:
                    // Nothing todo, already in Color format
                    break;
                case TextureSurfaceFormat.Dxt3:
                    data = DecompressDxt3(
                        data,
                        (int)textureData.Width,
                        (int)textureData.Height
                    );
                    components = 4;
                    break;
                default:
                    throw new Exception($"Texture format {textureData.Format} not supported");
            }

            for (int y = 0; y < textureData.Height; y++)
            {
                for (int x = 0; x < textureData.Width; x++)
                {
                    int index = (y * (int)textureData.Width + x) * components;

                    byte r = data[index];
                    byte g = data[index + 1];
                    byte b = data[index + 2];
                    byte a = (components == 4) ? data[index + 3] : (byte)255; // Si son RGB, se asume A = 255

                    Color color = Color.FromArgb(a, r, g, b);

                    bitmap.SetPixel(x, y, color);
                }
            }

            pictureBox1.Image = bitmap;
        }

        static byte[] DecompressDxt3(byte[] data, int width, int height)
        {
            byte[] output = new byte[width * height * 4];
            int blockIndex = 0;

            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    ulong alphaData = BitConverter.ToUInt64(data, blockIndex);
                    ushort color0 = BitConverter.ToUInt16(data, blockIndex + 8);
                    ushort color1 = BitConverter.ToUInt16(data, blockIndex + 10);
                    uint colorIndices = BitConverter.ToUInt32(data, blockIndex + 12);

                    blockIndex += 16;

                    var colors = DecodeColors(color0, color1);

                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int pixelIndex = py * 4 + px;

                            int alpha = (int)((alphaData >> (pixelIndex * 4)) & 0xF);
                            alpha = (alpha << 4) | alpha; // 4 → 8 bits

                            int colorIndex = (int)((colorIndices >> (pixelIndex * 2)) & 0x3);
                            var c = colors[colorIndex];

                            int outX = x + px;
                            int outY = y + py;

                            if (outX < width && outY < height)
                            {
                                int outIdx = (outY * width + outX) * 4;
                                output[outIdx + 0] = c.R;
                                output[outIdx + 1] = c.G;
                                output[outIdx + 2] = c.B;
                                output[outIdx + 3] = (byte)alpha;
                            }
                        }
                    }
                }
            }

            return output;
        }

        static Color[] DecodeColors(ushort c0, ushort c1)
        {
            Color[] colors = new Color[4];

            colors[0] = Rgb565ToColor(c0);
            colors[1] = Rgb565ToColor(c1);

            colors[2] = Color.FromArgb(
                (2 * colors[0].R + colors[1].R) / 3,
                (2 * colors[0].G + colors[1].G) / 3,
                (2 * colors[0].B + colors[1].B) / 3
            );

            colors[3] = Color.FromArgb(
                (colors[0].R + 2 * colors[1].R) / 3,
                (colors[0].G + 2 * colors[1].G) / 3,
                (colors[0].B + 2 * colors[1].B) / 3
            );

            return colors;
        }

        static Color Rgb565ToColor(ushort value)
        {
            int r = ((value >> 11) & 0x1F) << 3;
            int g = ((value >> 5) & 0x3F) << 2;
            int b = (value & 0x1F) << 3;

            return Color.FromArgb(r, g, b);
        }

        public void Export()
        {
            var bitmap = (Bitmap)pictureBox1.Image;
            using (var sfd = new SaveFileDialog())
            {
                var isPng = Data.Components == 4;
                sfd.Filter = isPng ? "PNG|*.png" : "JPG|*.jpg";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    bitmap.Save(sfd.FileName, isPng ? System.Drawing.Imaging.ImageFormat.Png : System.Drawing.Imaging.ImageFormat.Jpeg);
                }
            }
        }
    }
}
