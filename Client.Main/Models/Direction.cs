using Microsoft.Xna.Framework;

namespace Client.Main.Models
{
    public enum Direction
    {
        West = 0,
        SouthWest = 1,
        South = 2,
        SouthEast = 3,
        East = 4,
        NorthEast = 5,
        North = 6,
        NorthWest = 7
    }

    public static class DirectionExtensions
    {
        public static Vector3 ToAngle(this Direction direction)
        {
            return direction switch
            {
                Direction.West => new Vector3(0, 0, MathHelper.ToRadians(-45)),
                Direction.SouthWest => new Vector3(0, 0, MathHelper.ToRadians(0)),
                Direction.South => new Vector3(0, 0, MathHelper.ToRadians(45)),
                Direction.SouthEast => new Vector3(0, 0, MathHelper.ToRadians(90)),
                Direction.East => new Vector3(0, 0, MathHelper.ToRadians(135)),
                Direction.NorthEast => new Vector3(0, 0, MathHelper.ToRadians(180)),
                Direction.North => new Vector3(0, 0, MathHelper.ToRadians(225)),
                Direction.NorthWest => new Vector3(0, 0, MathHelper.ToRadians(270)),
                _ => Vector3.Zero,
            };
        }

        public static Direction FromAngle(float angleRadians)
        {
            // Normalize angle to be between 0 and 2*PI
            angleRadians = MathHelper.WrapAngle(angleRadians);
            if (angleRadians < 0) angleRadians += MathHelper.TwoPi;

            // Convert to degrees for easier division (0-360)
            float degrees = MathHelper.ToDegrees(angleRadians);

            // Each direction covers 45 degrees. Offset by 22.5 to center the zones.
            int index = (int)System.Math.Floor((degrees + 22.5f) / 45.0f) % 8;

            // Order: W(0), SW(1), S(2), SE(3), E(4), NE(5), N(6), NW(7)
            // Client uses different order. This mapping is for server-like directions if needed.
            // For client facing, it might be simpler:
            // Angle 0 (East in XNA) might be North-East on map.
            // This needs careful checking against how player Angle.Z is interpreted visually.
            return (Direction)index; // This is a direct mapping. Adjust if visual rotation differs. //TODO: wrong angle
        }
    }
}
