using Client.Main.Controls;
using Client.Main.Models;
using LEA;
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
        private List<Vector2> _currentPath;

        public Vector2 Location { get => _location; set => OnLocationChanged(_location, value); }
        public Direction Direction { get => _direction; set { _direction = value; OnDirectionChanged(); } }

        public Vector3 TargetPosition
        {
            get
            {
                var x = Location.X * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var y = Location.Y * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var v = new Vector3(x, y, World.Terrain.RequestTerrainHeight(x, y));
                return v;
            }
        }
        public Vector3 MoveTargetPosition { get; private set; }
        public float MoveSpeed { get; set; } = 250f;
        public bool IsMoving => Vector3.Distance(MoveTargetPosition, TargetPosition) > 0f;

        public override async Task Load()
        {
            MoveTargetPosition = Vector3.Zero;
            await base.Load();
        }

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
            MoveCameraPosition(gameTime);

            if (_targetAngle != Angle)
                Angle = Vector3.Lerp(Angle, _targetAngle, 0.3f);

            if (_currentPath != null && _currentPath.Count > 0 && !IsMoving)
            {
                Vector2 nextStep = _currentPath[0];
                MoveTowards(nextStep, gameTime);
                _currentPath.RemoveAt(0);
            }

            Position = MoveTargetPosition + new Vector3(0, 0, World.Terrain.RequestTerrainHeight(TargetPosition.X, TargetPosition.Y) - 40);

            base.Update(gameTime);
        }

        public void MoveTo(Vector2 targetLocation)
        {
            List<Vector2> path = Pathfinding.FindPath(Location, targetLocation, World);
            if (path == null) return;
            _currentPath = path;
        }

        private void MoveCameraPosition(GameTime time)
        {
            if (MoveTargetPosition == Vector3.Zero)
            {
                MoveTargetPosition = TargetPosition;
                UpdateCameraPosition(MoveTargetPosition);
                return;
            }

            if (!IsMoving)
            {
                MoveTargetPosition = TargetPosition;
                return;
            }

            Vector3 direction = TargetPosition - MoveTargetPosition;
            direction.Normalize();

            float deltaTime = (float)time.ElapsedGameTime.TotalSeconds;
            Vector3 moveVector = direction * MoveSpeed * deltaTime;

            // Verifica si la distancia a mover excede la distancia restante al objetivo
            if (moveVector.Length() > (MoveTargetPosition - TargetPosition).Length())
            {
                UpdateCameraPosition(TargetPosition);
            }
            else
            {
                UpdateCameraPosition(MoveTargetPosition + moveVector);
            }
        }

        private void UpdateCameraPosition(Vector3 position)
        {
            MoveTargetPosition = position;

            var cameraDistance = 1000f;

            var p = new Vector3(0, -cameraDistance, 0f);
            var m = MathUtils.AngleMatrix(new Vector3(0, 0, -48.5f));
            var t = MathUtils.VectorIRotate(p, m);

            Camera.Instance.FOV = 35;
            Camera.Instance.Position = position + t + new Vector3(0, 0, cameraDistance);
            Camera.Instance.Target = position;
        }

        private void MoveTowards(Vector2 target, GameTime gameTime)
        {
            //Vector2 direction = target - Location;
            //direction.Normalize();

            //float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Location = target;
            // Location += direction * MoveSpeed * deltaTime;
        }
    }
}
