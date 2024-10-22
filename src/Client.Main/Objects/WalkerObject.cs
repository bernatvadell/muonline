﻿using Client.Main.Models;
using Microsoft.Xna.Framework;
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
        private float currentCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private float targetCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private const float minCameraDistance = Constants.MIN_CAMERA_DISTANCE;
        private const float maxCameraDistance = Constants.MAX_CAMERA_DISTANCE;
        private const float zoomSpeed = Constants.ZOOM_SPEED;
        private int previousScrollValue = 0;

        // Camera rotation variables
        private float cameraYaw = Constants.CAMERA_YAW;
        private float cameraPitch = Constants.CAMERA_PITCH;
        private const float rotationSensitivity = Constants.ROTATION_SENSITIVITY;
        private bool isRotating = false;
        private Point lastMousePosition;

        // Default camera values
        private const float defaultCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private static readonly float defaultCameraPitch = Constants.DEFAULT_CAMERA_PITCH;
        private const float defaultCameraYaw = Constants.DEFAULT_CAMERA_YAW;

        // Rotation limits
        private static readonly float maxPitch = Constants.MAX_PITCH;
        private static readonly float minPitch = Constants.MIN_PITCH;

        // Mouse state tracking
        private ButtonState previousMiddleButtonState = ButtonState.Released;
        private bool wasRotating = false;

        public Vector2 Location
        {
            get => _location;
            set => OnLocationChanged(_location, value);
        }

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

            var mouseState = Mouse.GetState();
            previousScrollValue = mouseState.ScrollWheelValue;
            previousMiddleButtonState = mouseState.MiddleButton;

            cameraYaw = defaultCameraYaw;
            cameraPitch = defaultCameraPitch;

            await base.Load();
        }

        public void Reset()
        {
            _currentPath = null;
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
            HandleMouseInput();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            currentCameraDistance = MathHelper.Lerp(currentCameraDistance, targetCameraDistance, zoomSpeed * deltaTime);
            currentCameraDistance = MathHelper.Clamp(currentCameraDistance, minCameraDistance, maxCameraDistance);

            MoveCameraPosition(gameTime);

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

            if (_currentPath != null && _currentPath.Count > 0 && !IsMoving)
            {
                Vector2 nextStep = _currentPath[0];
                MoveTowards(nextStep, gameTime);
                _currentPath.RemoveAt(0);
            }

            // Calculate target height based on terrain and scaling
            float baseHeightOffset = 0; // Offset
            float heightScaleFactor = 0.5f;
            float terrainHeightAtMoveTarget = World.Terrain.RequestTerrainHeight(MoveTargetPosition.X, MoveTargetPosition.Y);
            float desiredHeightOffset = baseHeightOffset + (heightScaleFactor * terrainHeightAtMoveTarget);
            float targetHeight = terrainHeightAtMoveTarget + desiredHeightOffset;

            // Interpolation using Lerp
            float interpolationFactor = 15f * deltaTime; // factor
            float newZ = MathHelper.Lerp(Position.Z, targetHeight, interpolationFactor);

            // update position with the new height
            Position = new Vector3(MoveTargetPosition.X, MoveTargetPosition.Y, newZ);

            // Update camera position with rotation
            UpdateCameraPosition(MoveTargetPosition);

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

            // Calculate camera offset using spherical coordinates
            float x = currentCameraDistance * (float)Math.Cos(cameraPitch) * (float)Math.Sin(cameraYaw);
            float y = currentCameraDistance * (float)Math.Cos(cameraPitch) * (float)Math.Cos(cameraYaw);
            float z = currentCameraDistance * (float)Math.Sin(cameraPitch);

            Vector3 cameraOffset = new Vector3(x, y, z);

            // Calculate camera position
            Vector3 cameraPosition = position + cameraOffset;

            Camera.Instance.FOV = 35;
            Camera.Instance.Position = cameraPosition;
            Camera.Instance.Target = position;
        }

        private void MoveTowards(Vector2 target, GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float speed = MoveSpeed * deltaTime;

            Vector2 direction = target - Location;
            float distance = direction.Length();

            if (distance <= speed)
            {
                Location = target;
            }
            else
            {
                direction.Normalize();
                Location += direction * speed;
            }
        }

        private void HandleMouseInput()
        {
            MouseState mouseState = Mouse.GetState();

            // Handle mouse scroll for zooming
            int currentScroll = mouseState.ScrollWheelValue;
            int scrollDifference = currentScroll - previousScrollValue;

            if (scrollDifference != 0)
            {
                float zoomChange = scrollDifference / 120f * 100f; // 100 units for each scroll notch
                targetCameraDistance -= zoomChange;
                targetCameraDistance = MathHelper.Clamp(targetCameraDistance, minCameraDistance, maxCameraDistance);
            }

            previousScrollValue = currentScroll;

            // Handle middle mouse button
            if (mouseState.MiddleButton == ButtonState.Pressed)
            {
                if (!isRotating)
                {
                    isRotating = true;
                    lastMousePosition = mouseState.Position;
                    wasRotating = false; // Reset flag before starting rotation
                }
                else
                {
                    // Calculate mouse movement
                    Point currentMousePosition = mouseState.Position;
                    Vector2 mouseDelta = (currentMousePosition - lastMousePosition).ToVector2();

                    if (mouseDelta.LengthSquared() > 0) // Check for actual movement
                    {
                        // Adjust camera rotation
                        cameraYaw -= mouseDelta.X * rotationSensitivity;
                        cameraPitch += mouseDelta.Y * rotationSensitivity;

                        // Clamp vertical rotation to prevent camera flipping
                        cameraPitch = MathHelper.Clamp(cameraPitch, minPitch, maxPitch);

                        // Wrap horizontal rotation
                        cameraYaw = MathHelper.WrapAngle(cameraYaw);

                        wasRotating = true; // Flag that rotation occurred

                        lastMousePosition = currentMousePosition;
                    }
                }
            }
            else if (mouseState.MiddleButton == ButtonState.Released && previousMiddleButtonState == ButtonState.Pressed)
            {
                // Reset camera only if there was no rotation (i.e., just a click)
                if (!wasRotating)
                {
                    // Reset to default values
                    targetCameraDistance = defaultCameraDistance;
                    cameraYaw = defaultCameraYaw;
                    cameraPitch = defaultCameraPitch;
                }

                isRotating = false;
                wasRotating = false;
            }

            previousMiddleButtonState = mouseState.MiddleButton;
        }
    }
}
