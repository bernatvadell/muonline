﻿using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class WalkerObject : ModelObject
    {
        // Fields
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

        private CancellationTokenSource _autoIdleCts;
        private const float RotationSpeed = 8;

        // Properties
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

        public Vector3 MoveTargetPosition { get; set; }
        public float MoveSpeed { get; set; } = Constants.MOVE_SPEED;
        public bool IsMoving => Vector3.Distance(MoveTargetPosition, TargetPosition) > 0f;
        public ushort NetworkId { get; set; }

        // Public Methods
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

        public void OnDirectionChanged()
        {
            if (World is WalkableWorldControl)
                _targetAngle = _direction.ToAngle();
            else
                Angle = _direction.ToAngle();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // 1) Camera and zoom control for the local player
            if (IsMainWalker)
                HandleMouseInput();

            // 2) Update camera position and height interpolation
            UpdatePosition(gameTime);

            // 3) Execute the next step of the path (_currentPath is set in MoveTo)
            if (_currentPath != null && _currentPath.Count > 0 && !IsMoving)
            {
                var next = _currentPath[0];
                MoveTowards(next, gameTime);
                _currentPath.RemoveAt(0);
            }

            if (!IsMoving)
            {
                if (this is MonsterObject) // Monsters
                {
                    const byte MonsterWalk = 2; // 2 = "walk"
                    const byte MonsterIdle = 1; // 1 = "idle"

                    if (CurrentAction == MonsterWalk && Model?.Actions?.Length > MonsterIdle)
                    {
                        CurrentAction = MonsterIdle;
                    }
                }
                else // Local or remote player
                {
                    const byte PlayerWalk = 1; // 1 = "walk" (male/female)
                    const byte PlayerIdle = 0; // 0 = "stop/idle"

                    if (CurrentAction == PlayerWalk && Model?.Actions?.Length > PlayerIdle)
                    {
                        CurrentAction = PlayerIdle;
                    }
                }
            }
        }

        /// <summary>
        /// Plays the specified action.
        /// For monsters/NPCs, automatically returns to idle
        /// when the animation finishes and the object is stationary.
        /// </summary>
        public void PlayAction(byte actionIndex)
        {
            // --- Nothing to do ---
            if (CurrentAction == actionIndex)
                return;

            CurrentAction = actionIndex;

            // --- Local player and remote players without auto-idle ---
            if (this is not MonsterObject)
                return;

            // Idle(1) and Walk(2) do not need reset
            if (actionIndex is 1 or 2)
                return;

            // --- Animation length from NumAnimationKeys ---
            var actions = Model?.Actions;
            if (actions == null || actionIndex >= actions.Length)
                return;

            var act = actions[actionIndex];
            int frames = act?.NumAnimationKeys ?? 0;
            if (frames <= 0)
                return;

            // FPS – if not in model, assume 10 fps
            float fps = 10f; // Default FPS
            if (fps <= 0f) fps = 10f;

            int msTotal = (int)(1000f * frames / fps) + 100; // +small margin

            // --- Cancel previous timer ---
            _autoIdleCts?.Cancel();
            _autoIdleCts = new CancellationTokenSource();
            var token = _autoIdleCts.Token;

            // --- Start one-time "timer" ---
            _ = Task.Delay(msTotal, token).ContinueWith(_ =>
            {
                if (token.IsCancellationRequested) return;

                MuGame.ScheduleOnMainThread(() =>
                {
                    // If still stationary and action hasn't changed in the meantime
                    if (!IsMoving && CurrentAction == actionIndex)
                    {
                        const byte MonsterIdle = 1;
                        if (Model?.Actions?.Length > MonsterIdle)
                            CurrentAction = MonsterIdle;
                    }
                });
            }, token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
        }

        public void MoveTo(Vector2 targetLocation, bool sendToServer = true)
        {
            if (World == null)
                return;

            // Find path only for adjacent tiles
            List<Vector2> path = Pathfinding.FindPath(
                new Vector2((int)Location.X, (int)Location.Y),
                targetLocation,
                World);

            if (path == null || path.Count == 0)
                return;

            _currentPath = path;

            /*  *** ONLY LOCAL PLAYER SENDS PACKETS ***  */
            if (sendToServer && IsMainWalker)
            {
                SendWalkPathToServer(path);
            }
        }

        // Private Methods
        private void OnLocationChanged(Vector2 oldLocation, Vector2 newLocation)
        {
            if (oldLocation == newLocation) return;
            _location = new Vector2((int)newLocation.X, (int)newLocation.Y);

            if (oldLocation == Vector2.Zero)
                return;

            var oldX = oldLocation.X;
            var oldY = oldLocation.Y;
            var newX = newLocation.X;
            var newY = newLocation.Y;

            if (newX < oldX && newY < oldY) Direction = Direction.West;
            else if (newX == oldX && newY < oldY) Direction = Direction.SouthWest;
            else if (newX > oldX && newY < oldY) Direction = Direction.South;
            else if (newX < oldX && newY == oldY) Direction = Direction.NorthWest;
            else if (newX > oldX && newY == oldY) Direction = Direction.SouthEast;
            else if (newX < oldX && newY > oldY) Direction = Direction.North;
            else if (newX == oldX && newY > oldY) Direction = Direction.NorthEast;
            else if (newX > oldX && newY > oldY) Direction = Direction.East;
        }

        private void UpdatePosition(GameTime gameTime)
        {
            if (World is not WalkableWorldControl walkableWorld)
                return;

            UpdateMoveTargetPosition(gameTime);

            float worldExtraHeight = walkableWorld.ExtraHeight;
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _currentCameraDistance = MathHelper.Lerp(
                _currentCameraDistance,
                _targetCameraDistance,
                _zoomSpeed * deltaTime);
            _currentCameraDistance = MathHelper.Clamp(
                _currentCameraDistance,
                _minCameraDistance,
                _maxCameraDistance);

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
            float heightScaleFactor = 0.5f;
            float terrainHeightAtMoveTarget = MoveTargetPosition.Z + worldExtraHeight + ExtraHeight;
            float desiredHeightOffset = heightScaleFactor * terrainHeightAtMoveTarget;
            float targetHeight = terrainHeightAtMoveTarget + desiredHeightOffset;

            // Interpolation using Lerp
            float interpolationFactor = 15f * deltaTime; // Factor
            float newZ = MathHelper.Lerp(Position.Z, targetHeight, interpolationFactor);

            // Update position with the new height
            Position = new Vector3(MoveTargetPosition.X, MoveTargetPosition.Y, newZ);
        }

        private async void SendWalkPathToServer(List<Vector2> path)
        {
            if (path == null || path.Count == 0) return;
            var net = MuGame.Network;
            if (net == null) return;

            // 1) StartX/StartY: current client position
            byte startX = (byte)Location.X;
            byte startY = (byte)Location.Y;

            // 2) Function returning CLIENT CODE (0-7) according to MU Online documentation
            //    W=0, SW=1, S=2, SE=3, E=4, NE=5, N=6, NW=7
            static byte GetClientDirectionCode(Vector2 from, Vector2 to)
            {
                int dx = (int)(to.X - from.X); // Horizontal (X): left / right – works correctly
                int dy = (int)(to.Y - from.Y); // Vertical (Y): up / down – correction here

                return (dx, dy) switch
                {
                    (-1, 0) => 0,  // West
                    (-1, 1) => 1,  // South-West
                    (0, 1) => 2,  // South
                    (1, 1) => 3,  // South-East
                    (1, 0) => 4,  // East
                    (1, -1) => 5,  // North-East
                    (0, -1) => 6,  // North
                    (-1, -1) => 7,  // North-West
                    _ => 0xFF      // Invalid direction
                };
            }

            // 3) Build list of client directions
            List<byte> clientDirs = new(path.Count);
            Vector2 currentPos = Location;

            foreach (var step in path)
            {
                byte dirCode = GetClientDirectionCode(currentPos, step);
                if (dirCode > 7) break; // Non-neighbor – end
                clientDirs.Add(dirCode);
                currentPos = step;
                if (clientDirs.Count == 15) break; // Max path length
            }

            if (clientDirs.Count == 0) return; // Clicked on the same tile

            // 4) TRANSLATE to server codes using DirectionMap from configuration
            var directionMap = MuGame.Network?.GetDirectionMap(); // IDictionary<byte, byte>
            List<byte> serverDirs = new List<byte>(clientDirs.Count);

            if (directionMap != null)
            {
                foreach (byte clientDir in clientDirs)
                {
                    if (directionMap.TryGetValue(clientDir, out byte serverDir))
                        serverDirs.Add(serverDir);
                    else
                        serverDirs.Add(clientDir); // Fallback if mapping not found (should not happen)
                }
            }
            else
            {
                serverDirs.AddRange(clientDirs); // Use client dirs if map is null
            }

            // 5) Send to server
            await net.SendWalkRequestAsync(startX, startY, serverDirs.ToArray());
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

            if (moveVector.Length() >= (TargetPosition - MoveTargetPosition).Length())
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
            // TODO: Fix this, it's not working properly on some maps when distance <= speed
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
                _targetCameraDistance = MathHelper.Clamp(
                    _targetCameraDistance,
                    _minCameraDistance,
                    _maxCameraDistance);
            }
            _previousScrollValue = currentScroll;

            // Handle middle mouse button for rotation and reset
            if (mouseState.MiddleButton == ButtonState.Pressed)
            {
                if (!_isRotating)
                {
                    _isRotating = true;
                    _wasRotating = false; // Reset flag before starting rotation
                }
                else
                {
                    Point currentMousePosition = mouseState.Position;
                    Vector2 mouseDelta = (currentMousePosition - MuGame.Instance.PrevMouseState.Position).ToVector2();

                    if (mouseDelta.LengthSquared() > 0) // Check for actual movement
                    {
                        _cameraYaw -= mouseDelta.X * _rotationSensitivity;
                        _cameraPitch -= mouseDelta.Y * _rotationSensitivity;
                        _cameraPitch = MathHelper.Clamp(_cameraPitch, _minPitch, _maxPitch); // Clamp vertical
                        _cameraYaw = MathHelper.WrapAngle(_cameraYaw); // Wrap horizontal
                        _wasRotating = true; // Flag that rotation occurred
                    }
                }
            }
            else if (mouseState.MiddleButton == ButtonState.Released &&
                     MuGame.Instance.PrevMouseState.MiddleButton == ButtonState.Pressed)
            {
                // Reset camera only if there was no rotation (i.e., just a click)
                if (!_wasRotating)
                {
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