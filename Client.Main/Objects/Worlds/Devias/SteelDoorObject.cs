using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Client.Main.Controllers;

namespace Client.Main.Objects.Worlds.Devias
{
    public class SteelDoorObject : ModelObject
    {
        // --- Common fields ---
        private bool _isOpen = false;
        private SteelDoorObject _pairedDoor = null;

        // --- Fields for rotating doors ---
        private bool _isRotating = false;
        private const float ROTATION_DURATION = 2f;
        private float _rotationTimer = 0f;
        private const float ROTATION_PROXIMITY = 3f;
        private float _startAngle = 0f;
        private float _targetAngle = 0f;
        public int RotationDirection { get; set; } = 1;

        // --- Fields for sliding doors (type 86) ---
        private bool _isSlidingDoor = false;
        private bool _isSlidingAnimating = false;
        private const float SLIDING_DURATION = 2f;
        private float _slidingTimer = 0f;
        private Vector3 _closedPosition;   // Closed position – set during loading
        private Vector3 _openPosition;     // Open position – calculated during pairing
        private Vector3 _startPosition;    // Starting position for sliding animation
        private Vector3 _targetPosition;   // Target position for animation
        public Vector3 SlidingDirection { get; set; } // Sliding direction
        private const float SLIDING_DISTANCE = 200f;    // Sliding distance – adjust as needed
        private bool _isFullyLoaded = false;            // Flag to prevent pairing before load completes

        // --- Dictionary of pairs (for rotating doors, e.g., 88/65) ---
        // For other types (e.g., 20 or 86), pair doors of the same type.
        private static readonly Dictionary<int, int> DoorPairs = new()
        {
            { 88, 65 },
        };

        public override async Task Load()
        {
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;
            Model = await BMDLoader.Instance.Prepare($"Object3/Object{Type + 1}.bmd");
            await base.Load();

            // Determine if these are sliding doors (e.g., type 86)
            _isSlidingDoor = (Type == 86);
            if (_isSlidingDoor)
            {
                // Assume that doors are closed during loading
                _closedPosition = Position;
            }

            _isFullyLoaded = true;
            FindPairedDoor();
        }

        private void FindPairedDoor()
        {
            if (_pairedDoor != null || !(World is WalkableWorldControl walkableWorld))
                return;

            // For rotating doors – if a mapping exists, use it; otherwise, pair doors of the same type.
            int targetType = Type;
            if (!_isSlidingDoor)
            {
                if (DoorPairs.TryGetValue(Type, out int mappedType))
                    targetType = mappedType;
            }
            // For sliding doors (type 86), pair doors of the same type.

            // Find another door that meets the criteria:
            // - a different object than this,
            // - of type targetType,
            // - not yet paired,
            // - fully loaded (prevents using uninitialized _closedPosition),
            // - located within a certain radius.
            var nearbyDoor = walkableWorld.Objects
                .OfType<SteelDoorObject>()
                .FirstOrDefault(door =>
                    door != this &&
                    door.Type == targetType &&
                    door._pairedDoor == null &&
                    door._isFullyLoaded &&
                    Vector2.Distance(
                        new Vector2(Position.X, Position.Y),
                        new Vector2(door.Position.X, door.Position.Y)
                    ) < 500f); // Distance threshold – adjust as needed

            if (nearbyDoor != null)
            {
                _pairedDoor = nearbyDoor;
                nearbyDoor._pairedDoor = this;

                if (_isSlidingDoor)
                {
                    // --- Pairing for sliding doors (type 86) ---
                    // Calculate the vector connecting the doors, ignoring the Z axis (movement occurs on the XY plane)
                    Vector3 diff = nearbyDoor.Position - this.Position;
                    Vector3 diffXY = new Vector3(diff.X, diff.Y, 0);
                    if (diffXY != Vector3.Zero)
                    {
                        Vector3 normDiff = Vector3.Normalize(diffXY);
                        // We determine that these doors slide in opposite directions:
                        // - this door: in the opposite direction to the connecting vector,
                        // - partner door: along the vector.
                        this.SlidingDirection = -normDiff;
                        nearbyDoor.SlidingDirection = normDiff;
                    }
                    else
                    {
                        // When the doors are exactly in the same position – set default directions
                        this.SlidingDirection = new Vector3(1, 0, 0);
                        nearbyDoor.SlidingDirection = new Vector3(-1, 0, 0);
                    }

                    // Calculate the open position: closed position shifted by SLIDING_DISTANCE along the sliding direction.
                    _openPosition = _closedPosition + SlidingDirection * SLIDING_DISTANCE;
                    nearbyDoor._openPosition = nearbyDoor._closedPosition + nearbyDoor.SlidingDirection * SLIDING_DISTANCE;
                }
                else
                {
                    // --- Pairing for rotating doors ---
                    const float epsilon = 0.1f;
                    float normalizedAngle = Angle.Z % MathHelper.TwoPi;
                    if (Math.Abs(normalizedAngle) < epsilon || Math.Abs(normalizedAngle - MathHelper.TwoPi) < epsilon)
                    {
                        RotationDirection = -1;
                        nearbyDoor.RotationDirection = 1;
                    }
                    else if (Math.Abs(normalizedAngle - MathHelper.Pi) < epsilon)
                    {
                        RotationDirection = 1;
                        nearbyDoor.RotationDirection = -1;
                    }
                    else
                    {
                        if (Position.X < nearbyDoor.Position.X)
                        {
                            RotationDirection = -1;
                            nearbyDoor.RotationDirection = 1;
                        }
                        else
                        {
                            RotationDirection = 1;
                            nearbyDoor.RotationDirection = -1;
                        }
                    }
                }
            }
        }

        #region Rotation Animation (rotating doors)
        private void StartRotation(bool open)
        {
            _isRotating = true;
            _rotationTimer = 0f;
            _startAngle = Angle.Z;
            float rotationAmount = MathHelper.PiOver2 * RotationDirection;
            _targetAngle = open ? _startAngle + rotationAmount : _startAngle - rotationAmount;

            if (_pairedDoor != null)
                _pairedDoor.StartRotationInternal(open);

            if (Type == 86)
                SoundController.Instance.PlayBuffer("Sound/aCastleDoor.wav");
            else
                SoundController.Instance.PlayBuffer("Sound/aDoor.wav");
        }

        private void StartRotationInternal(bool open)
        {
            if (_isRotating)
                return;

            _isRotating = true;
            _rotationTimer = 0f;
            _startAngle = Angle.Z;
            float rotationAmount = MathHelper.PiOver2 * RotationDirection;
            _targetAngle = open ? _startAngle + rotationAmount : _startAngle - rotationAmount;
        }

        private void UpdateRotation(GameTime gameTime)
        {
            if (_isRotating)
            {
                _rotationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Min(_rotationTimer / ROTATION_DURATION, 1f);
                float smoothProgress = (float)(1 - Math.Cos(progress * Math.PI)) / 2f;
                float currentAngle = MathHelper.Lerp(_startAngle, _targetAngle, smoothProgress);

                Angle = new Vector3(Angle.X, Angle.Y, currentAngle);

                if (progress >= 1f)
                {
                    Angle = new Vector3(Angle.X, Angle.Y, _targetAngle);
                    _isRotating = false;
                    _isOpen = !_isOpen;
                }
            }
        }
        #endregion

        #region Sliding Animation (sliding doors, type 86)
        private void StartSliding(bool open)
        {
            _isSlidingAnimating = true;
            _slidingTimer = 0f;
            _startPosition = Position;
            _targetPosition = open ? _openPosition : _closedPosition;

            if (_pairedDoor != null)
                _pairedDoor.StartSlidingInternal(open);
        }

        private void StartSlidingInternal(bool open)
        {
            if (_isSlidingAnimating)
                return;

            _isSlidingAnimating = true;
            _slidingTimer = 0f;
            _startPosition = Position;
            _targetPosition = open ? _openPosition : _closedPosition;
        }

        private void UpdateSliding(GameTime gameTime)
        {
            if (_isSlidingAnimating)
            {
                _slidingTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Min(_slidingTimer / SLIDING_DURATION, 1f);
                float smoothProgress = (float)(1 - Math.Cos(progress * Math.PI)) / 2f;
                Position = Vector3.Lerp(_startPosition, _targetPosition, smoothProgress);

                if (progress >= 1f)
                {
                    Position = _targetPosition;
                    _isSlidingAnimating = false;
                    _isOpen = !_isOpen;
                }
            }
        }
        #endregion

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // If the pair hasn't been found yet, try again
            if (_pairedDoor == null)
                FindPairedDoor();

            // Get the player's position (assuming that the door and player coordinates are in the same scale)
            Vector2 playerPosition2D = Vector2.Zero;
            if (World is WalkableWorldControl walkableWorld)
                playerPosition2D = walkableWorld.Walker.Location;

            Vector2 thisDoorPosition = new Vector2(Position.X / 100f, Position.Y / 100f);
            float distanceToThisDoor = Vector2.Distance(playerPosition2D, thisDoorPosition);

            float distanceToPairedDoor = float.MaxValue;
            if (_pairedDoor != null)
            {
                Vector2 pairedDoorPosition = new Vector2(_pairedDoor.Position.X / 100f, _pairedDoor.Position.Y / 100f);
                distanceToPairedDoor = Vector2.Distance(playerPosition2D, pairedDoorPosition);
            }

            bool playerInProximity = distanceToThisDoor < ROTATION_PROXIMITY || distanceToPairedDoor < ROTATION_PROXIMITY;

            if (_isSlidingDoor)
            {
                // Only allow sliding if doors are properly paired and _openPosition is calculated
                if (_pairedDoor != null && _openPosition != Vector3.Zero)
                {
                    if (!_isSlidingAnimating && !_isOpen && playerInProximity)
                        StartSliding(true);
                    else if (!_isSlidingAnimating && _isOpen && !playerInProximity)
                        StartSliding(false);
                }

                UpdateSliding(gameTime);
                // Removed redundant update call for paired door
            }
            else
            {
                if (!_isRotating && !_isOpen && playerInProximity)
                    StartRotation(true);
                else if (!_isRotating && _isOpen && !playerInProximity)
                    StartRotation(false);

                UpdateRotation(gameTime);
                _pairedDoor?.UpdateRotation(gameTime);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
