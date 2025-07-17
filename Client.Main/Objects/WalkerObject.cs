using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class WalkerObject : ModelObject
    {
        // Fields: rotation and movement
        private Vector3 _targetAngle;
        private Direction _direction;
        private Vector2 _location;
        protected Queue<Vector2> _currentPath;   // FIFO – cheaper removal than List.RemoveAt(0)

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

        // private CancellationTokenSource _autoIdleCts; // Now managed by AnimationController
        private const float RotationSpeed = 8f;
        private int _previousActionForSound = -1;
        private bool _serverControlledAnimation = false;

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
        public new ushort NetworkId { get; set; }

        public ushort idanim = 0;

        // Public Methods
        protected readonly AnimationController _animationController;

        public bool IsOneShotPlaying => _animationController?.IsOneShotPlaying ?? false;

        protected WalkerObject()
        {
            _animationController = new AnimationController(this);
        }

        public new virtual async Task Load()
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
            
            // Reset animation state to clear any stuck death animations
            _animationController?.Reset();
        }

        /// <summary>
        /// Immediately stops any ongoing movement for this walker.
        /// Clears the current path and locks the movement target to the
        /// current world position so the object stays exactly where it is.
        /// The logical <see cref="Location"/> is also synchronized without
        /// triggering direction changes.
        /// </summary>
        public void StopMovement()
        {
            _currentPath?.Clear();
            _currentPath = null;

            // Freeze the object at its current rendered position
            MoveTargetPosition = Position;

            // Update the logical tile position without invoking OnLocationChanged
            _location = new Vector2(
                (int)(Position.X / Constants.TERRAIN_SCALE),
                (int)(Position.Y / Constants.TERRAIN_SCALE));

            // Align target angle with current rotation to prevent snapping
            _targetAngle = Angle;
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

            // --- animation test ---
            // if (IsMainWalker)
            // {
            //     KeyboardState currentKeyboardState = Keyboard.GetState();
            //     if (currentKeyboardState.IsKeyDown(Keys.A) && _previousKeyboardState_WalkerTest.IsKeyUp(Keys.A))
            //     {
            //         idanim += 1;
            //         Debug.WriteLine($"[WALKER_ANIM_TEST] Key 'A' pressed. Changing animation to ID: {idanim}");
            //         PlayAction((ushort)idanim);
            //     }

            //     if (currentKeyboardState.IsKeyDown(Keys.B) && _previousKeyboardState_WalkerTest.IsKeyUp(Keys.B)) 
            //     {
            //         idanim -= 1;
            //         Debug.WriteLine($"[WALKER_ANIM_TEST] Key 'A' pressed. Changing animation to ID: {idanim}");
            //         PlayAction((ushort)idanim);
            //     }
            //     _previousKeyboardState_WalkerTest = currentKeyboardState;
            // }
            // -----------------------------------------


            if (IsMainWalker)
                HandleMouseInput();

            UpdatePosition(gameTime);

            // Call the animation update method
            Animation(gameTime);

            if (_currentPath != null && _currentPath.Count > 0 && !IsMoving)
            {
                var next = _currentPath.Dequeue();
                MoveTowards(next, gameTime);
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
        /// Advances the current animation and builds bone matrices for this frame.
        /// </summary>
        private void Animation(GameTime gameTime)
        {
            // Fast exits ──────────────────────────────────────────────────────
            if (LinkParentAnimation) return;
            if (Model?.Actions == null || Model.Actions.Length == 0) return;

            int actionIdx = CurrentAction;
            if (actionIdx < 0 || actionIdx >= Model.Actions.Length)
            {
                actionIdx = 0;
                if (actionIdx >= Model.Actions.Length) return;
            }

            var action = Model.Actions[actionIdx];
            int totalFrames = Math.Max(action.NumAnimationKeys, 1);

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float objFps = AnimationSpeed;
            float playMul = action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed;
            float effectiveFps = Math.Max(0.01f, objFps * playMul);

            AnimationType animType = _animationController.GetAnimationType((ushort)actionIdx);

            // Reset animation time when switching actions
            if (_priorAction != actionIdx)
                _animTime = 0.0;

            //------------------------------------------------------------------
            // Frame position calculation
            //------------------------------------------------------------------
            double framePos;

            if (animType == AnimationType.Death)
            {
                // Keep advancing but clamp to second-to-last key to hold the pose
                int endIdx = Math.Max(0, totalFrames - 2);
                _animTime += delta * effectiveFps;
                _animTime = Math.Min(_animTime, endIdx + 0.0001f);
                framePos = _animTime;
            }
            else if (animType is AnimationType.Attack or AnimationType.Skill or AnimationType.Emote)
            {
                if (_animationController.IsOneShotPlaying)
                {
                    _animTime += delta * effectiveFps;

                    if (_animTime >= totalFrames - 1.0f)
                    {
                        _animationController.NotifyAnimationCompleted();
                        framePos = totalFrames - 0.0001f; // last key
                    }
                    else
                    {
                        framePos = _animTime;
                    }
                }
                else
                {
                    framePos = 0; // one-shot not playing
                }
            }
            else // Looping (Idle / Walk / Rest / Sit)
            {
                _animTime += delta * effectiveFps;
                framePos = _animTime % totalFrames;
            }

            //------------------------------------------------------------------
            // Key selection & interpolation
            //------------------------------------------------------------------
            int f0 = Math.Max(0, (int)framePos);
            int f1 = (totalFrames > 1) ? ((f0 + 1) % totalFrames) : f0;
            float t = (float)(framePos - f0);

            // Clamp indices (safety)
            f0 = Math.Min(f0, totalFrames - 1);
            f1 = Math.Min(f1, totalFrames - 1);

            //------------------------------------------------------------------
            // Build the final bone matrices
            //------------------------------------------------------------------
            GenerateBoneMatrix(actionIdx, f0, f1, t);
            _priorAction = actionIdx;
        }

        /// <summary>
        /// Plays the specified action using the centralized animation controller.
        /// </summary>
        public void PlayAction(ushort actionIndex, bool fromServer = false)
        {
            _serverControlledAnimation = fromServer;
            _animationController?.PlayAnimation(actionIndex, fromServer);
        }

        // Overload for backward compatibility
        public void PlayAction(ushort actionIndex)
        {
            PlayAction(actionIndex, false);
        }

        public virtual void MoveTo(Vector2 targetLocation, bool sendToServer = true)
        {
            if (World == null) return;
            
            // Don't allow movement if player is dead
            if (!this.IsAlive()) return;

            if (this is PlayerObject player)
            {
                player.OnPlayerMoved();
            }

            Vector2 startPos = new Vector2((int)Location.X, (int)Location.Y);
            WorldControl currentWorld = World;
            _ = Task.Run(() =>
            {
                List<Vector2> path = Pathfinding.FindPath(startPos, targetLocation, currentWorld);

                // If no path was found for a remote object, fall back to a simple
                // straight-line path so that the character still moves visibly
                if ((path == null || path.Count == 0) && !sendToServer)
                {
                    path = Pathfinding.BuildDirectPath(startPos, targetLocation);
                }

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (MuGame.Instance.ActiveScene?.World != currentWorld || this.Status == GameControlStatus.Disposed)
                        return;

                    if (path == null || path.Count == 0)
                    {
                        _currentPath?.Clear();
                        return;
                    }

                    _currentPath = new Queue<Vector2>(path);

                    if (sendToServer && IsMainWalker)
                    {
                        Task.Run(() => SendWalkPathToServerAsync(path));
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

            // stackalloc: no GC pressure for  ≤15-step MU packet
            Span<byte> clientDirs = stackalloc byte[15];
            int dirLen = 0;
            Vector2 currentPos = Location;
            foreach (var step in path)
            {
                byte dirCode = GetClientDirectionCode(currentPos, step);
                if (dirCode > 7) break;
                clientDirs[dirLen++] = dirCode;
                currentPos = step;
                if (dirLen == 15) break;
            }
            if (dirLen == 0) return;

            var directionMap = MuGame.Network?.GetDirectionMap();
            Span<byte> serverDirs = stackalloc byte[dirLen];
            for (int i = 0; i < dirLen; i++)
            {
                byte cd = clientDirs[i];
                serverDirs[i] = directionMap != null && directionMap.TryGetValue(cd, out byte sd) ? sd : cd;
            }

            // Network API requires array – copy once, still cheaper than per-step List allocations
            await net.SendWalkRequestAsync(startX, startY, serverDirs.ToArray());
        }

        // Private Methods
        protected virtual void OnLocationChanged(Vector2 oldLocation, Vector2 newLocation)
        {
            if (oldLocation == newLocation) return;
            _location = new Vector2((int)newLocation.X, (int)newLocation.Y);

            if (oldLocation == Vector2.Zero)
                return;

            var oldX = oldLocation.X;
            var oldY = oldLocation.Y;
            var newX = newLocation.X;
            var newY = newLocation.Y;

            // Use helper that already maps delta → Direction enum
            Direction = DirectionExtensions.GetDirectionFromMovementDelta(
                            (int)(newLocation.X - oldLocation.X),
                            (int)(newLocation.Y - oldLocation.Y));
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
            {
                UpdateCameraPosition(TargetPosition);
                _movementIntent = false;
            }
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
#if ANDROID
            Camera.Instance.FOV *= Constants.ANDROID_FOV_SCALE;
#endif
            Camera.Instance.Position = cameraPosition;
            Camera.Instance.Target = position;
        }

        private void MoveTowards(Vector2 target, GameTime gameTime)
        {
            Location = target;

            if (this is MonsterObject monster)
            {
                // Check if a one-shot animation is currently playing
                if (_animationController?.IsOneShotPlaying == true)
                {
                    // Don't override the animation if a one-shot is playing
                    // Debug.WriteLine($"[WalkerObject] Monster one-shot animation is playing, not overriding with walk animation");
                    return;
                }

                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk;
                int moveAction = MONSTER_ACTION_WALK;
                if (Model?.Actions?.Length > (int)MonsterActionType.Run &&
                    Model.Actions[(int)MonsterActionType.Run]?.NumAnimationKeys > 0)
                {
                    // Optionally choose run instead of walk here
                }

                if (CurrentAction != moveAction)
                {
                    // Debug.WriteLine($"[WalkerObject] Monster starting walk animation: {moveAction}");
                    PlayAction((byte)moveAction);
                }
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
        
        protected bool _movementIntent;
        public bool MovementIntent => _movementIntent;

        // protected override void Dispose(bool disposing)
        // {
        //     if (disposing)
        //     {
        //         _animationController?.Dispose();
        //     }
        //     base.Dispose(disposing);
        // }
    }
}