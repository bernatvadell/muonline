using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Graphics
{
    /// <summary>
    /// Vertex format for GPU skinning path (single-bone influence per vertex).
    /// Bone index is passed as TEXCOORD1 to keep compatibility with custom effects.
    /// </summary>
    public struct SkinnedVertexPositionColorNormalTexture : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public float BoneIndex;

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(16, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(36, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        public SkinnedVertexPositionColorNormalTexture(
            Vector3 position,
            Color color,
            Vector3 normal,
            Vector2 textureCoordinate,
            float boneIndex)
        {
            Position = position;
            Color = color;
            Normal = normal;
            TextureCoordinate = textureCoordinate;
            BoneIndex = boneIndex;
        }
    }
}
