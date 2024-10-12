using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class WalkerObject : ModelObject
    {
        private Vector3 _targetAngle;
        private Direction _direction;
        private Vector2 _location;


        public new WalkableWorldControl World { get => (WalkableWorldControl)base.World; }
        public Vector2 Location { get => _location; set => OnLocationChanged(_location, value); }
        public Direction Direction { get => _direction; set { _direction = value; OnDirectionChanged(); } }

        private void OnDirectionChanged()
        {
            _targetAngle = _direction.ToAngle();
        }

        private void OnLocationChanged(Vector2 oldLocation, Vector2 newLocation)
        {
            if (oldLocation == newLocation) return;
            _location = newLocation;

            if (newLocation.X < oldLocation.X && newLocation.Y < oldLocation.Y)
                Direction = Direction.West;
            else if (newLocation.X == oldLocation.X && newLocation.Y < oldLocation.Y)
                Direction = Direction.SouthWest;
            else if (newLocation.X > oldLocation.X && newLocation.Y < oldLocation.Y)
                Direction = Direction.South;
            else if (newLocation.X < oldLocation.X && newLocation.Y == oldLocation.Y)
                Direction = Direction.NorthWest;
            else if (newLocation.X > oldLocation.X && newLocation.Y == oldLocation.Y)
                Direction = Direction.SouthEast;
            else if (newLocation.X < oldLocation.X && newLocation.Y > oldLocation.Y)
                Direction = Direction.North;
            else if (newLocation.X == oldLocation.X && newLocation.Y > oldLocation.Y)
                Direction = Direction.NorthEast;
            else if (newLocation.X > oldLocation.X && newLocation.Y > oldLocation.Y)
                Direction = Direction.East;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_targetAngle != Angle)
            {
                Angle = Vector3.Lerp(Angle, _targetAngle, 0.3f);
            }
        }
    }
}
