using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

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
                Direction.NorthWest => new Vector3(0, 0, MathHelper.ToRadians(280)),
                _ => Vector3.Zero,
            };
        }
    }
}
