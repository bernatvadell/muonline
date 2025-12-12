using Client.Data.BMD;
using Microsoft.Xna.Framework;

namespace Client.Main.Content
{
    /// <summary>
    /// Allows objects to procedurally deform skinned vertices during buffer generation.
    /// </summary>
    public interface IVertexDeformer
    {
        Vector3 DeformVertex(in BMDTextureVertex vertex, in Vector3 transformedPosition);
    }
}

