using System.IO;

namespace Client.Data.BMD
{
    public class BMDTextureMesh
    {
        public BMDTextureVertex[] Vertices { get; set; } = [];
        public BMDTextureNormal[] Normals { get; set; } = [];
        public BMDTexCoord[] TexCoords { get; set; } = [];
        public BMDTriangle[] Triangles { get; set; } = [];
        public short Texture { get; set; } = 0;
        public string TexturePath { get; set; } = string.Empty;

        //for custom blending from json
        public string BlendingMode { get; set; } = null;
    }
}
