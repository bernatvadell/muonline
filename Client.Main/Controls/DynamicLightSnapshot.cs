using Microsoft.Xna.Framework;

namespace Client.Main.Controls
{
    /// <summary>
    /// Immutable snapshot of a dynamic light used for throttled lighting updates.
    /// </summary>
    public readonly struct DynamicLightSnapshot
    {
        public Vector3 Position { get; }
        public Vector3 Color { get; }
        public float Radius { get; }
        public float Intensity { get; }

        public DynamicLightSnapshot(Vector3 position, Vector3 color, float radius, float intensity)
        {
            Position = position;
            Color = color;
            Radius = radius;
            Intensity = intensity;
        }
    }
}

