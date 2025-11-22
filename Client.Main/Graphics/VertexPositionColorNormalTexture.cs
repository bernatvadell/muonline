using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Graphics
{
    /// <summary>
    /// Vertex layout that carries position, baked color, normal, and texture coordinates.
    /// Matches the input expected by DynamicLighting.fx.
    /// </summary>
    public struct TerrainVertexPositionColorNormalTexture : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;

        public TerrainVertexPositionColorNormalTexture(Vector3 position, Color color, Vector3 normal, Vector2 texCoord)
        {
            Position = position;
            Color = color;
            Normal = normal;
            TextureCoordinate = texCoord;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(16, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
