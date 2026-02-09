using Microsoft.Xna.Framework;

namespace Client.Main.Content
{
    /// <summary>
    /// Allows objects to procedurally modify texture coordinates during buffer generation.
    /// Implement alongside IVertexDeformer and return from GetVertexDeformer().
    /// BMDLoader will detect the interface and apply UV transformations.
    /// </summary>
    public interface ITexCoordDeformer
    {
        Vector2 DeformTexCoord(float u, float v);
    }
}
