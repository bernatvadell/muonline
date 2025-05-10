using Client.Main.Controls;
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
        // Fields: rotation and movement
        private Vector3 _targetAngle;
        private Direction _direction;
        private Vector2 _location;
        private List<Vector2> _currentPath;

        // Camera control
        private float _currentCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private float _targetCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private const float _minCameraDistance = Constants.MIN_CAMERA_DISTANCE;
        private const float _maxCameraDistance = Constants.MAX_CAMERA_DISTANCE;
        private const float _zoomSpeed = Constants.ZOOM_SPEED;
        private int _previousScrollValue;

        // Camera rotation
        private float _cameraYaw = Constants.CAMERA_YAW;
        private float _cameraPitch = Constants.CAMERA_PITCH;
        private const float _rotationSensitivity = Constants.ROTATION_SENSITIVITY;
        private bool _isRotating;
        private bool _wasRotating;

        // Default camera angles
        private const float _defaultCameraDistance = Constants.DEFAULT_CAMERA_DISTANCE;
        private static readonly float _defaultCameraPitch = Constants.DEFAULT_CAMERA_PITCH;
        private const float _defaultCameraYaw = Constants.DEFAULT_CAMERA_YAW;

        // Rotation limits
        private static readonly float _maxPitch = Constants.MAX_PITCH;
        private static readonly float _minPitch = Constants.MIN_PITCH;

        private CancellationTokenSource _autoIdleCts;
        private const float RotationSpeed = 8f;
        private int _previousActionForSound = -1;

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

            if (IsMainWalker)
                HandleMouseInput();

            UpdatePosition(gameTime);

            if (_currentPath != null && _currentPath.Count > 0 && !IsMoving)
            {
                var next = _currentPath[0];
                MoveTowards(next, gameTime);
                _currentPath.RemoveAt(0);
            }

            if (CurrentAction != _previousActionForSound)
            {
                if (this is MonsterObject)
                {
                    // Monster-specific sound methods are called in PlayAction
                }
                _previousActionForSound = CurrentAction;
            }
        }

        /// <summary>
        /// Plays the specified action. For monsters/NPCs, automatically returns to idle
        /// when the animation finishes and the object is stationary.
        /// </summary>
        public void PlayAction(ushort actionIndex)
        {
            if (Model?.Actions == null || actionIndex >= Model.Actions.Length)
                return;

            var act = Model.Actions[actionIndex];

            bool isOneShot = this is MonsterObject m
                             && actionIndex != (ushort)MonsterActionType.Stop1
                             && actionIndex != (ushort)MonsterActionType.Stop2
                             && actionIndex != (ushort)MonsterActionType.Walk
                             && actionIndex != (ushort)MonsterActionType.Run
                             && actionIndex != (ushort)MonsterActionType.Die;

            if (!isOneShot && CurrentAction == actionIndex)
                return;

            CurrentAction = actionIndex;
            InvalidateBuffers();

            _autoIdleCts?.Cancel();
            _autoIdleCts?.Dispose();
            _autoIdleCts = null;

            if (isOneShot && act.NumAnimationKeys > 1)
            {
                float objFps = AnimationSpeed;
                float playMul = act.PlaySpeed == 0 ? 1.0f : act.PlaySpeed;
                float effectiveFps = Math.Max(0.1f, objFps * playMul);

                int frames = act.NumAnimationKeys;
                int delayMs = (int)((frames / effectiveFps) * 1000f) + 100;
                delayMs = Math.Clamp(delayMs, 50, 60_000);

                _autoIdleCts = new CancellationTokenSource();
                var token = _autoIdleCts.Token;

                Task.Delay(delayMs, token).ContinueWith(t =>
                {
                    if (t.IsCanceled || token.IsCancellationRequested) return;

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        if (!IsMoving && CurrentAction == actionIndex)
                        {
                            byte idle = this is MonsterObject
                                        ? (byte)MonsterActionType.Stop1
                                        : (byte)PlayerAction.StopMale; //TODO:female

                            if (Model?.Actions != null && idle < Model.Actions.Length)
                                CurrentAction = idle;
                        }
                    });
                }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            }
        }

        public void MoveTo(Vector2 targetLocation, bool sendToServer = true)
        {
            if (World == null) return;

            Vector2 startPos = new Vector2((int)Location.X, (int)Location.Y);
            WorldControl currentWorld = World;
            _ = Task.Run(() =>
            {
                List<Vector2> path = Pathfinding.FindPath(startPos, targetLocation, currentWorld);

                if (MuGame.Instance.ActiveScene?.World != currentWorld || path == null || path.Count == 0)
                {
                    return;
                }

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World == currentWorld && this.Status != GameControlStatus.Disposed)
                    {
                        _currentPath = path;

                        if (sendToServer && IsMainWalker)
                        {
                            Task.Run(() => SendWalkPathToServerAsync(path));
                        }
                    }
                });
            });
        }

        private async Task SendWalkPathToServerAsync(List<Vector2> path)
        {
            if (path == null || path.Count == 0) return;
            var net = MuGame.Network;
            if (net == null) return;

            byte startX = (byte)Location.X;
            byte startY = (byte)Location.Y;

            //    Function returning CLIENT CODE (0-7) according to MU Online documentation
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

            List<byte> clientDirs = new(path.Count);
            Vector2 currentPos = Location;
            foreach (var step in path)
            {
                byte dirCode = GetClientDirectionCode(currentPos, step);
                if (dirCode > 7) break;
                clientDirs.Add(dirCode);
                currentPos = step;
                if (clientDirs.Count == 15) break;
            }
            if (clientDirs.Count == 0) return;

            var directionMap = MuGame.Network?.GetDirectionMap();
            List<byte> serverDirs = new(clientDirs.Count);
            if (directionMap != null)
            {
                foreach (byte clientDir in clientDirs)
                {
                    if (directionMap.TryGetValue(clientDir, out byte serverDir))
                        serverDirs.Add(serverDir);
                    else
                        serverDirs.Add(clientDir);
                }
            }
            else
            {
                serverDirs.AddRange(clientDirs);
            }

            await net.SendWalkRequestAsync(startX, startY, serverDirs.ToArray());
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
                    Angle = new Vector3(Angle.X, Angle.Y, _targetAngle.Z);
            }

            float heightScaleFactor = 0.5f;
            float terrainHeightAtMoveTarget = MoveTargetPosition.Z + worldExtraHeight + ExtraHeight;
            float desiredHeightOffset = heightScaleFactor * terrainHeightAtMoveTarget;
            float targetHeight = terrainHeightAtMoveTarget + desiredHeightOffset;

            float interpolationFactor = 15f * deltaTime;
            float newZ = MathHelper.Lerp(Position.Z, targetHeight, interpolationFactor);

            Position = new Vector3(MoveTargetPosition.X, MoveTargetPosition.Y, newZ);
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
                UpdateCameraPosition(TargetPosition);
            else
                UpdateCameraPosition(MoveTargetPosition + moveVector);
        }

        private void UpdateCameraPosition(Vector3 position)
        {
            MoveTargetPosition = position;
            if (!IsMainWalker) return;

            float x = _currentCameraDistance * (float)Math.Cos(_cameraPitch) * (float)Math.Sin(_cameraYaw);
            float y = _currentCameraDistance * (float)Math.Cos(_cameraPitch) * (float)Math.Cos(_cameraYaw);
            float z = _currentCameraDistance * (float)Math.Sin(_cameraPitch);
            var cameraOffset = new Vector3(x, y, z);
            var cameraPosition = position + cameraOffset;

            Camera.Instance.FOV = 35;
            Camera.Instance.Position = cameraPosition;
            Camera.Instance.Target = position;
        }

        private void MoveTowards(Vector2 target, GameTime gameTime)
        {
            Location = target;

            if (this is MonsterObject monster)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                int moveAction = MONSTER_ACTION_WALK;
                if (Model?.Actions?.Length > (int)MonsterActionType.Run &&
                    Model.Actions[(int)MonsterActionType.Run]?.NumAnimationKeys > 0)
                {
                    // Optionally choose run instead of walk here
                }

                if (CurrentAction != moveAction)
                    PlayAction((byte)moveAction);
            }
        }

        private void HandleMouseInput()
        {
            var mouseState = MuGame.Instance.Mouse;
            int currentScroll = mouseState.ScrollWheelValue;
            int scrollDiff = currentScroll - _previousScrollValue;
            if (scrollDiff != 0)
            {
                float zoomChange = scrollDiff / 120f * 100f;
                _targetCameraDistance = MathHelper.Clamp(
                    _targetCameraDistance - zoomChange,
                    _minCameraDistance,
                    _maxCameraDistance);
            }
            _previousScrollValue = currentScroll;

            if (mouseState.MiddleButton == ButtonState.Pressed)
            {
                if (!_isRotating)
                {
                    _isRotating = true;
                    _wasRotating = false;
                }
                else
                {
                    var delta = (mouseState.Position - MuGame.Instance.PrevMouseState.Position).ToVector2();
                    if (delta.LengthSquared() > 0)
                    {
                        _cameraYaw -= delta.X * _rotationSensitivity;
                        _cameraPitch = MathHelper.Clamp(_cameraPitch - delta.Y * _rotationSensitivity, _minPitch, _maxPitch);
                        _cameraYaw = MathHelper.WrapAngle(_cameraYaw);
                        _wasRotating = true;
                    }
                }
            }
            else if (mouseState.MiddleButton == ButtonState.Released &&
                     MuGame.Instance.PrevMouseState.MiddleButton == ButtonState.Pressed)
            {
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
