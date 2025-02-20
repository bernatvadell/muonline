
namespace Client.Data.Texture
{
    public class TextureData
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public byte Components { get; set; }
        public byte[] Data { get; set; } = [];
    }
}
