using Microsoft.Xna.Framework;

namespace Client.Main.Models
{
    public enum MiniMapMarkerKind
    {
        None = 0,
        NPC = 1,
        Portal = 2,
        // Add other kinds if needed
    }

    public class MiniMapMarker
    {
        public MiniMapMarkerKind Kind { get; set; }
        public Vector2 Location { get; set; } // World Tile Coordinates (0-255)
        public float Rotation { get; set; } // Degrees
        public string Name { get; set; } // For Tooltip
        public int ID { get; set; } // Unique ID for rendering/tooltip lookup
    }
}