using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Graphics
{
    public struct StaticModelInstanceData : IVertexType
    {
        public Matrix World;
        public Color Color;

        public StaticModelInstanceData(Matrix world, Color color)
        {
            World = world;
            Color = color;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
            new VertexElement(64, VertexElementFormat.Color, VertexElementUsage.Color, 1));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
