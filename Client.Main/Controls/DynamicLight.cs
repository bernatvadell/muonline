using Microsoft.Xna.Framework;
using Client.Main.Objects;

namespace Client.Main.Controls
{
    /// <summary>
    /// Simple dynamic light source used for terrain and object lighting.
    /// </summary>
    public class DynamicLight
    {
        /// <summary>The WorldObject that owns this light source.</summary>
        public WorldObject Owner { get; set; }

        /// <summary>World position of the light.</summary>
        public Vector3 Position { get; set; }

        /// <summary>RGB color of the light in the 0..1 range.</summary>
        public Vector3 Color { get; set; } = Vector3.One;

        /// <summary>Effective radius of the light.</summary>
        public float Radius { get; set; } = 200f;

        /// <summary>Light intensity multiplier.</summary>
        public float Intensity { get; set; } = 1f;
    }
}