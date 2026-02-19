using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Graphics
{
    /// <summary>
    /// Vertex format for grass wind deformation in vertex shader.
    /// TEXCOORD1 packs: (dirX, dirY, phase, amplitude).
    /// </summary>
    public struct GrassVertexPositionColorTextureWind : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector2 TextureCoordinate;
        public Vector4 WindData;

        public GrassVertexPositionColorTextureWind(
            Vector3 position,
            Color color,
            Vector2 textureCoordinate,
            Vector4 windData)
        {
            Position = position;
            Color = color;
            TextureCoordinate = textureCoordinate;
            WindData = windData;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
