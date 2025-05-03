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

        public void OnDirectionChanged()
        {
            if (World is WalkableWorldControl)
                _targetAngle = _direction.ToAngle();
            else
                Angle = _direction.ToAngle();
        }

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
            if (World is not WalkableWorldControl walkableWorld)
                return;

            UpdateMoveTargetPosition(gameTime);

            float worldExtraHeight = walkableWorld.ExtraHeight;
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
            float heightScaleFactor = 0.5f;
            float terrainHeightAtMoveTarget = MoveTargetPosition.Z + worldExtraHeight + ExtraHeight;
            float desiredHeightOffset = heightScaleFactor * terrainHeightAtMoveTarget;
            float targetHeight = terrainHeightAtMoveTarget + desiredHeightOffset;

            // Interpolation using Lerp
            float interpolationFactor = 15f * deltaTime; // factor
            float newZ = MathHelper.Lerp(Position.Z, targetHeight, interpolationFactor);

            // update position with the new height
            Position = new Vector3(MoveTargetPosition.X, MoveTargetPosition.Y, newZ);
        }

        public void MoveTo(Vector2 targetLocation, bool sendToServer = true)
        {
            if (World == null)
                return;

            // znajdź ścieżkę tylko dla sąsiadujących pól
            List<Vector2> path = Pathfinding.FindPath(
                new Vector2((int)Location.X, (int)Location.Y),
                targetLocation,
                World);

            if (path == null || path.Count == 0)
                return;

            _currentPath = path;

            /*  *** TYLKO GRACZ-LOKALNY WYSYŁA PAKIETY ***  */
            if (sendToServer && IsMainWalker)
            {
                SendWalkPathToServer(path);
            }
        }

        private async void SendWalkPathToServer(List<Vector2> path)
        {
            if (path == null || path.Count == 0) return;
            var net = MuGame.Network;
            if (net == null) return;

            // 1) StartX/StartY: obecna pozycja klienta
            byte startX = (byte)Location.X;
            byte startY = (byte)Location.Y;

            // 2) Funkcja zwracająca KOD KLIENTA (0-7) wg dokumentacji MU Online
            //    W =0, SW =1, S =2, SE =3, E =4, NE =5, N =6, NW =7
            static byte Dir(Vector2 from, Vector2 to)
            {
                int dx = (int)(to.X - from.X); // poziomo (X): lewo / prawo – działa poprawnie
                int dy = (int)(to.Y - from.Y); // pionowo (Y): góra / dół – poprawka tutaj

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
                    _ => 0xFF
                };
            }


            // 3) Zbuduj listę kierunków klienta
            List<byte> dirs = new(path.Count);
            Vector2 cur = Location;

            foreach (var step in path)
            {
                byte d = Dir(cur, step);
                if (d > 7) break;          // nie-sąsiad – koniec
                dirs.Add(d);
                cur = step;
                if (dirs.Count == 15) break;
            }

            if (dirs.Count == 0) return;   // klik w to samo pole

            // 4) PRZETŁUMACZ na kody serwera przy użyciu DirectionMap z konfiguracji
            // ------------------------------------------------------------------
            // DirectionMap: 0→7, 1→6, 2→5, 3→4, 4→3, 5→2, 6→1, 7→0
            // znajduje się w  net.GetDirectionMap()  (przykład)
            // ------------------------------------------------------------------
            var map = MuGame.Network?.GetDirectionMap();    // IDictionary<byte, byte>
            if (map != null)
            {
                for (int i = 0; i < dirs.Count; i++)
                {
                    if (map.TryGetValue(dirs[i], out byte srvDir))
                        dirs[i] = srvDir;
                }
            }

            // 5) Wyślij do serwera
            await net.SendWalkRequestAsync(startX, startY, dirs.ToArray());
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
                        _cameraPitch -= mouseDelta.Y * _rotationSensitivity;

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
