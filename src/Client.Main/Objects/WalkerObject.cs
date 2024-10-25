using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class WalkerObject : ModelObject
    {
        // Rotation and movement variables
        private Vector3 _targetAngle;
        private Direction _direction;
        private Vector2 _location;
        private List<Vector2> _currentPath;

        // Camera control variables
        private float _currentCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private float _targetCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private const float _minCameraDistance = Constants.MIN_CAMERA_DISTANCE;
        private const float _maxCameraDistance = Constants.MAX_CAMERA_DISTANCE;
        private const float _zoomSpeed = Constants.ZOOM_SPEED;
        private int _previousScrollValue = 0;

        // Camera rotation variables
        private float _cameraYaw = Constants.CAMERA_YAW;
        private float _cameraPitch = Constants.CAMERA_PITCH;
        private const float _rotationSensitivity = Constants.ROTATION_SENSITIVITY;
        private bool _isRotating = false;

        // Default camera values
        private const float _defaultCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private static readonly float _defaultCameraPitch = Constants.DEFAULT_CAMERA_PITCH;
        private const float _defaultCameraYaw = Constants.DEFAULT_CAMERA_YAW;

        // Rotation limits
        private static readonly float _maxPitch = Constants.MAX_PITCH;
        private static readonly float _minPitch = Constants.MIN_PITCH;

        // Mouse state tracking
        private bool _wasRotating = false;

        public bool IsMainWalker => World is WalkableWorldControl walkableWorld && walkableWorld.Walker == this;

        public Vector2 Location
        {
            get => _location;
            set => OnLocationChanged(_location, value);
        }

        public float ExtraHeight { get; set; }

        public Direction Direction
        {
            get => _direction;
            set
            {
                if (_direction != value)
                {
                    _direction = value;
                    OnDirectionChanged();
                }
            }
        }

        public Vector3 TargetPosition
        {
            get
            {
                var x = Location.X * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var y = Location.Y * Constants.TERRAIN_SCALE + 0.5f * Constants.TERRAIN_SCALE;
                var z = World.Terrain.RequestTerrainHeight(x, y);
                return new Vector3(x, y, z);
            }
        }

        public Vector3 MoveTargetPosition { get; private set; }
        public float MoveSpeed { get; set; } = Constants.MOVE_SPEED;
        public bool IsMoving => Vector3.Distance(MoveTargetPosition, TargetPosition) > 0f;

        private const float RotationSpeed = 8;

        public override async Task Load()
        {
            MoveTargetPosition = Vector3.Zero;
            _previousScrollValue = MuGame.Instance.Mouse.ScrollWheelValue;
            _cameraYaw = _defaultCameraYaw;
            _cameraPitch = _defaultCameraPitch;
            await base.Load();
        }

        public void Reset()
        {
            _currentPath = null;
            MoveTargetPosition = Vector3.Zero;
        }

        private void OnDirectionChanged()
        {
            _targetAngle = _direction.ToAngle();
        }

        private void OnLocationChanged(Vector2 oldLocation, Vector2 newLocation)
        {
            if (oldLocation == newLocation) return;
            _location = new Vector2((int)newLocation.X, (int)newLocation.Y);

            var oldX = oldLocation.X;
            var oldY = oldLocation.Y;

            var newX = newLocation.X;
            var newY = newLocation.Y;

            if (newX < oldX && newY < oldY)
                Direction = Direction.West;
            else if (newX == oldX && newY < oldY)
                Direction = Direction.SouthWest;
            else if (newX > oldX && newY < oldY)
                Direction = Direction.South;
            else if (newX < oldX && newY == oldY)
                Direction = Direction.NorthWest;
            else if (newX > oldX && newY == oldY)
                Direction = Direction.SouthEast;
            else if (newX < oldX && newY > oldY)
                Direction = Direction.North;
            else if (newX == oldX && newY > oldY)
                Direction = Direction.NorthEast;
            else if (newX > oldX && newY > oldY)
                Direction = Direction.East;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsMainWalker)
                HandleMouseInput();

            UpdatePosition(gameTime);

            if (_currentPath != null && _currentPath.Count > 0 && !IsMoving)
            {
                Vector2 nextStep = _currentPath[0];
                MoveTowards(nextStep, gameTime);
                _currentPath.RemoveAt(0);
            }
        }

        private void UpdatePosition(GameTime gameTime)
        {
            float worldExtraHeight = 0f;

            UpdateMoveTargetPosition(gameTime);
            worldExtraHeight = ((WalkableWorldControl)World).ExtraHeight;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _currentCameraDistance = MathHelper.Lerp(_currentCameraDistance, _targetCameraDistance, _zoomSpeed * deltaTime);
            _currentCameraDistance = MathHelper.Clamp(_currentCameraDistance, _minCameraDistance, _maxCameraDistance);

            if (_targetAngle != Angle)
            {
                float localDeltaTime = deltaTime;

                Vector3 angleDifference = _targetAngle - Angle;
                angleDifference.Z = MathHelper.WrapAngle(angleDifference.Z);

                float maxStep = RotationSpeed * localDeltaTime;

                float stepZ = MathHelper.Clamp(angleDifference.Z, -maxStep, maxStep);

                Angle = new Vector3(Angle.X, Angle.Y, Angle.Z + stepZ);

                if (Math.Abs(angleDifference.Z) <= maxStep)
                {
                    Angle = new Vector3(Angle.X, Angle.Y, _targetAngle.Z);
                }
            }

            // Calculate target height based on terrain and scaling
            float baseHeightOffset = 0; // Offset
            float heightScaleFactor = 0.5f;
            float terrainHeightAtMoveTarget = World.Terrain.RequestTerrainHeight(MoveTargetPosition.X, MoveTargetPosition.Y) + worldExtraHeight + ExtraHeight;
            float desiredHeightOffset = baseHeightOffset + (heightScaleFactor * terrainHeightAtMoveTarget);
            float targetHeight = terrainHeightAtMoveTarget + desiredHeightOffset;

            // Interpolation using Lerp
            float interpolationFactor = 15f * deltaTime; // factor
            float newZ = MathHelper.Lerp(Position.Z, targetHeight, interpolationFactor);

            // update position with the new height
            Position = new Vector3(MoveTargetPosition.X, MoveTargetPosition.Y, newZ);
        }

        public void MoveTo(Vector2 targetLocation)
        {
            List<Vector2> path = Pathfinding.FindPath(new Vector2((int)Location.X, (int)Location.Y), targetLocation, World);
            if (path == null) return;
            _currentPath = path;
        }

        private void UpdateMoveTargetPosition(GameTime time)
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
                UpdateCameraPosition(MoveTargetPosition);
                return;
            }

            Vector3 direction = TargetPosition - MoveTargetPosition;
            direction.Normalize();

            float deltaTime = (float)time.ElapsedGameTime.TotalSeconds;
            Vector3 moveVector = direction * MoveSpeed * deltaTime;

            if (moveVector.Length() > (TargetPosition - MoveTargetPosition).Length())
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

            if (!IsMainWalker)
                return;

            // Calculate camera offset using spherical coordinates
            float x = _currentCameraDistance * (float)Math.Cos(_cameraPitch) * (float)Math.Sin(_cameraYaw);
            float y = _currentCameraDistance * (float)Math.Cos(_cameraPitch) * (float)Math.Cos(_cameraYaw);
            float z = _currentCameraDistance * (float)Math.Sin(_cameraPitch);

            Vector3 cameraOffset = new Vector3(x, y, z);

            // Calculate camera position
            Vector3 cameraPosition = position + cameraOffset;

            Camera.Instance.FOV = 35;
            Camera.Instance.Position = cameraPosition;
            Camera.Instance.Target = position;
        }

        private void MoveTowards(Vector2 target, GameTime gameTime)
        {
            Location = target;

            // TODO: Need to fix this, it's not working properly on some maps when distance <= speed

            //float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            //float speed = MoveSpeed * deltaTime;

            //Vector2 direction = target - Location;
            //float distance = direction.Length();

            //if (distance <= speed)
            //{
            //    Location = target;
            //}
            //else
            //{
            //    direction.Normalize();
            //    Location += direction * speed;
            //}
        }

        private void HandleMouseInput()
        {
            MouseState mouseState = MuGame.Instance.Mouse;

            // Handle mouse scroll for zooming
            int currentScroll = mouseState.ScrollWheelValue;
            int scrollDifference = currentScroll - _previousScrollValue;

            if (scrollDifference != 0)
            {
                float zoomChange = scrollDifference / 120f * 100f; // 100 units for each scroll notch
                _targetCameraDistance -= zoomChange;
                _targetCameraDistance = MathHelper.Clamp(_targetCameraDistance, _minCameraDistance, _maxCameraDistance);
            }

            _previousScrollValue = currentScroll;

            // Handle middle mouse button
            if (mouseState.MiddleButton == ButtonState.Pressed)
            {
                if (!_isRotating)
                {
                    _isRotating = true;
                    _wasRotating = false; // Reset flag before starting rotation
                }
                else
                {
                    // Calculate mouse movement
                    Point currentMousePosition = mouseState.Position;
                    Vector2 mouseDelta = (currentMousePosition - MuGame.Instance.PrevMouseState.Position).ToVector2();

                    if (mouseDelta.LengthSquared() > 0) // Check for actual movement
                    {
                        // Adjust camera rotation
                        _cameraYaw -= mouseDelta.X * _rotationSensitivity;
                        _cameraPitch += mouseDelta.Y * _rotationSensitivity;

                        // Clamp vertical rotation to prevent camera flipping
                        _cameraPitch = MathHelper.Clamp(_cameraPitch, _minPitch, _maxPitch);

                        // Wrap horizontal rotation
                        _cameraYaw = MathHelper.WrapAngle(_cameraYaw);

                        _wasRotating = true; // Flag that rotation occurred
                    }
                }
            }
            else if (mouseState.MiddleButton == ButtonState.Released && MuGame.Instance.PrevMouseState.MiddleButton == ButtonState.Pressed)
            {
                // Reset camera only if there was no rotation (i.e., just a click)
                if (!_wasRotating)
                {
                    // Reset to default values
                    _targetCameraDistance = _defaultCameraDistance;
                    _cameraYaw = _defaultCameraYaw;
                    _cameraPitch = _defaultCameraPitch;
                }

                _isRotating = false;
                _wasRotating = false;
            }
        }
    }
}
