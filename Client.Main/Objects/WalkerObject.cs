using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Monsters;
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
        protected List<Vector2> _currentPath;

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
        public ushort NetworkId { get; set; }

        public ushort idanim = 0;
        private KeyboardState _previousKeyboardState_WalkerTest;

        private bool _isDead = false;

        /// <summary>
        /// Indicates that the object has finished its death animation.
        /// </summary>
        public bool IsDead => _isDead;

        // Public Methods
        protected AnimationController _animationController;

        public bool IsOneShotPlaying => _animationController?.IsOneShotPlaying ?? false;

        public override async Task Load()
        {
            _animationController = new AnimationController(this);
            MoveTargetPosition = Vector3.Zero;
            _previousScrollValue = MuGame.Instance.Mouse.ScrollWheelValue;
            _cameraYaw = _defaultCameraYaw;
            _cameraPitch = _defaultCameraPitch;

            await base.Load();
        }

        // Updated Reset method
        public void Reset()
        {
            _currentPath = null;
            MoveTargetPosition = Vector3.Zero;
            _deathAnimationLocked = false;
            _isDead = false;
            _lockedDeathAction = -1;
            _lockedAnimTime = 0;
            Debug.WriteLine($"[WalkerObject] Reset called - unlocking death animation for {this.GetType().Name}");
        }

        // Updated UnlockDeathAnimation method
        public void UnlockDeathAnimation()
        {
            _deathAnimationLocked = false;
            _isDead = false;
            _lockedDeathAction = -1;
            _lockedAnimTime = 0;
            Debug.WriteLine($"[WalkerObject] Death animation manually unlocked for {this.GetType().Name}");
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

        // Add these fields to your WalkerObject class
        private double _deathAnimationStartTime = 0;
        private int _lockedDeathAction = -1; // Store the death action ID
        private double _lockedAnimTime = 0; // Store the locked animation time

        // Updated Animation method
        private void Animation(GameTime gameTime)
        {
            if (LinkParentAnimation) return;
            if (Model?.Actions == null || Model.Actions.Length == 0) return;

            int currentActionIndex = CurrentAction;

            if (currentActionIndex < 0 || currentActionIndex >= Model.Actions.Length)
            {
                currentActionIndex = 0;
                if (currentActionIndex >= Model.Actions.Length) return;
            }

            var action = Model.Actions[currentActionIndex];
            int totalFrames = Math.Max(action.NumAnimationKeys, 1);

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float objFps = AnimationSpeed;
            float playMul = action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed;
            float effectiveFps = Math.Max(0.01f, objFps * playMul);

            AnimationType animType = _animationController.GetAnimationType((ushort)currentActionIndex);

            // CRITICAL FIX: If we're dead and locked, override everything
            if (_isDead && _deathAnimationLocked && _lockedDeathAction != -1)
            {
                // Force the action to be the death action
                currentActionIndex = _lockedDeathAction;
                if (currentActionIndex >= 0 && currentActionIndex < Model.Actions.Length)
                {
                    action = Model.Actions[currentActionIndex];
                    totalFrames = Math.Max(action.NumAnimationKeys, 1);
                    animType = AnimationType.Death;
                }

                // Use the locked animation time (last frame)
                double lockedFramePos = _lockedAnimTime;

                int lockedF0 = Math.Max(0, Math.Min((int)lockedFramePos, totalFrames - 1));
                int lockedF1 = lockedF0; // No interpolation when dead
                float lockedT = 0f;

                GenerateBoneMatrix(currentActionIndex, lockedF0, lockedF1, lockedT);
                Debug.WriteLine($"[WalkerObject] Death animation - LOCKED on frame {lockedF0} for {this.GetType().Name}");
                return; // Exit early, don't process any other animation logic
            }

            // Reset animation time when action changes
            if (_priorAction != currentActionIndex)
            {
                _animTime = 0.0;
                Debug.WriteLine($"[WalkerObject] Animation change: {_priorAction} → {currentActionIndex} ({animType})");
            }

            double framePos;

            if (animType == AnimationType.Death)
            {
                if (_isDead)
                {
                    // Should not reach here if properly locked above
                    framePos = totalFrames - 0.0001f;
                    Debug.WriteLine($"[WalkerObject] Death animation - fallback holding last frame for {this.GetType().Name}");
                }
                else
                {
                    _animTime += delta * effectiveFps;

                    if (_animTime >= totalFrames - 0.0001f)
                    {
                        _animTime = totalFrames - 0.0001f;
                        _isDead = true;
                        _deathAnimationLocked = true;
                        _lockedDeathAction = currentActionIndex; // Lock the current death action
                        _lockedAnimTime = _animTime; // Lock the animation time

                        Debug.WriteLine($"[WalkerObject] Death animation completed for {this.GetType().Name} - LOCKING at frame {(int)_animTime}");
                    }

                    framePos = _animTime;
                }
            }
            else if (animType == AnimationType.Attack ||
                    animType == AnimationType.Skill ||
                    animType == AnimationType.Emote)
            {
                if (_animationController.IsOneShotPlaying)
                {
                    _animTime += delta * effectiveFps;

                    if (_animTime >= totalFrames - 1.0f)
                    {
                        _animationController?.NotifyAnimationCompleted();
                        framePos = totalFrames - 0.0001f;
                    }
                    else
                    {
                        framePos = _animTime;
                    }
                }
                else
                {
                    framePos = 0;
                }
            }
            else // Normal looping animations
            {
                _animTime += delta * effectiveFps;
                framePos = _animTime % totalFrames;
            }

            int f0 = Math.Max(0, (int)framePos);
            int f1 = f0;
            float t = 0f;

            if (animType == AnimationType.Walk || animType == AnimationType.Idle ||
                animType == AnimationType.Rest || animType == AnimationType.Sit ||
                animType == AnimationType.Death ||
                (IsOneShotPlaying && (animType == AnimationType.Attack || animType == AnimationType.Skill || animType == AnimationType.Emote)))
            {
                f1 = (totalFrames > 1) ? ((f0 + 1) % totalFrames) : f0;
                t = (float)(framePos - f0);
            }

            // For death animations that just completed, don't interpolate
            if (animType == AnimationType.Death && _isDead)
            {
                f0 = Math.Max(0, totalFrames - 1);
                f1 = f0;
                t = 0f;
            }

            // Clamp frame indices
            f0 = Math.Min(f0, totalFrames - 1);
            f1 = Math.Min(f1, totalFrames - 1);

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);
            _priorAction = currentActionIndex;
        }

        private bool _deathAnimationLocked = false; // NOWE POLE
        public void PlayAction(ushort actionIndex, bool fromServer = false)
        {
            _serverControlledAnimation = fromServer;
            var type = _animationController?.GetAnimationType(actionIndex) ?? AnimationType.Idle;
            var currentType = _animationController?.GetAnimationType((ushort)CurrentAction) ?? AnimationType.Idle;

            // CRITICAL: If we're dead and locked, reject ALL animation changes
            if (_isDead && _deathAnimationLocked)
            {
                Debug.WriteLine($"[WalkerObject] ALL animations blocked - object is dead and locked for {this.GetType().Name}");
                return;
            }

            if (type == AnimationType.Death)
            {
                // If death animation is already locked, don't restart
                if (_deathAnimationLocked)
                {
                    Debug.WriteLine($"[WalkerObject] Death animation blocked - already locked for {this.GetType().Name}");
                    return;
                }

                // If already playing the same death animation, don't restart
                if (currentType == AnimationType.Death && CurrentAction == actionIndex)
                {
                    Debug.WriteLine($"[WalkerObject] Death animation blocked - already playing same action for {this.GetType().Name}");
                    return;
                }

                Debug.WriteLine($"[WalkerObject] Starting death animation {actionIndex} for {this.GetType().Name}");
                _isDead = false;
                _deathAnimationLocked = false; // Will be set to true when animation completes
                _lockedDeathAction = -1;
                _lockedAnimTime = 0;
                _deathAnimationStartTime = _animTime;

                if (this is MonsterObject monster)
                {
                    monster.OnDeathAnimationStart();
                }
            }

            _animationController?.PlayAnimation(actionIndex, fromServer);
        }

        // Overload for backward compatibility
        public void PlayAction(ushort actionIndex)
        {
            PlayAction(actionIndex, false);
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
                        _animationController?.PlayAnimation((ushort)PlayerAction.WalkMale); // Or appropriate walk animation

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