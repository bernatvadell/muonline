using System;
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

        /// <summary>
        /// Maps a given angle (in radians) to the nearest Direction value.
        /// </summary>
        /// <param name="angle">Rotation angle in radians.</param>
        /// <returns>Nearest Direction enum value.</returns>
        public static Direction GetDirectionFromAngle(float angle)
        {
            // Convert angle from radians to degrees and normalize to [0,360)
            float degrees = MathHelper.ToDegrees(angle);
            if (degrees < 0)
                degrees += 360;

            // Add half step (22.5) and divide by 45 to determine the nearest index (0-7)
            int index = (int)Math.Floor((degrees + 22.5f) / 45f) % 8;

            // Mapping order: index 0: SouthWest (0°), 1: South (45°), 2: SouthEast (90°),
            // 3: East (135°), 4: NorthEast (180°), 5: North (225°), 6: NorthWest (270°), 7: West (315°)
            Direction[] mapping =
            [
                Direction.SouthWest,
                Direction.South,
                Direction.SouthEast,
                Direction.East,
                Direction.NorthEast,
                Direction.North,
                Direction.NorthWest,
                Direction.West
            ];

            return mapping[index];
        }

        /// <summary>
        /// Calculates the game direction from a 'from' tile to a 'to' tile.
        /// </summary>
        /// <param name="fromTile">The starting tile position.</param>
        /// <param name="toTile">The target tile position.</param>
        /// <returns>The Direction enum value.</returns>
        public static Direction GetDirectionFromTileDifference(Vector2 fromTile, Vector2 toTile)
        {
            int dx = (int)(toTile.X - fromTile.X);
            int dy = (int)(toTile.Y - fromTile.Y);

            // Normalize to ensure pure direction even if tiles are far apart
            int normDx = Math.Sign(dx);
            int normDy = Math.Sign(dy);

            return (normDx, normDy) switch
            {
                (-1, 0) => Direction.West,
                (-1, 1) => Direction.SouthWest,
                (0, 1) => Direction.South,
                (1, 1) => Direction.SouthEast,
                (1, 0) => Direction.East,
                (1, -1) => Direction.NorthEast,
                (0, -1) => Direction.North,
                (-1, -1) => Direction.NorthWest,
                // Default case if dx and dy are both 0 (e.g., target is on the same tile)
                // or for any other unexpected combination. Defaulting to South or current direction might be better.
                _ => Direction.South // Or handle as no change / keep current direction
            };
        }

        /// <summary>
        /// Determines the Direction enum based on a movement delta (targetTile - currentTile).
        /// This logic mirrors WalkerObject.OnLocationChanged's implicit mapping.
        /// Assumes standard tile map coordinates: +X is East (right), +Y is South (down).
        /// </summary>
        /// <param name="dx">Change in X tile coordinate (target.X - player.X).</param>
        /// <param name="dy">Change in Y tile coordinate (target.Y - player.Y).</param>
        /// <returns>The Direction enum value for visual facing.</returns>
        public static Direction GetDirectionFromMovementDelta(int dx, int dy)
        {
            // This mapping needs to be identical to the implicit mapping in WalkerObject.OnLocationChanged
            // when (newX - oldX) is dx and (newY - oldY) is dy.
            if (dx < 0 && dy < 0) return Direction.West;        // Moving/Facing grid NW -> Visual West (-45 deg / Top-Left screen)
            if (dx == 0 && dy < 0) return Direction.SouthWest;   // Moving/Facing grid N  -> Visual SouthWest (0 deg / Left-Down screen)
            if (dx > 0 && dy < 0) return Direction.South;       // Moving/Facing grid NE -> Visual South (45 deg / Down screen)
            if (dx < 0 && dy == 0) return Direction.NorthWest;   // Moving/Facing grid W  -> Visual NorthWest (270 deg / Left-Up screen)
            if (dx > 0 && dy == 0) return Direction.SouthEast;   // Moving/Facing grid E  -> Visual SouthEast (90 deg / Right-Down screen)
            if (dx < 0 && dy > 0) return Direction.North;       // Moving/Facing grid SW -> Visual North (225 deg / Up-Right screen)
            if (dx == 0 && dy > 0) return Direction.NorthEast;   // Moving/Facing grid S  -> Visual NorthEast (180 deg / Right-Up screen)
            if (dx > 0 && dy > 0) return Direction.East;        // Moving/Facing grid SE -> Visual East (135 deg / Up-Right screen corner)

            // Default if dx and dy are 0 (target is on the same tile as player)
            return Direction.South; // Or consider returning current direction, or another default.
        }
    }
}
