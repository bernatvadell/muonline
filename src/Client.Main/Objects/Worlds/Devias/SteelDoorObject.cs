using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Client.Main.Objects.Worlds.Devias
{
    public class SteelDoorObject : ModelObject
    {
        private bool _isRotating = false;
        private bool _isOpen = false;
        private const float ROTATION_DURATION = 2f;
        private float _rotationTimer = 0f;
        private const float ROTATION_PROXIMITY = 3f;
        private float _startAngle = 0f;
        private float _targetAngle = 0f;

        private SteelDoorObject _pairedDoor = null;

        public int RotationDirection { get; set; } = 1;

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

            FindPairedDoor();
        }

        private void FindPairedDoor()
        {
            if (_pairedDoor != null || !(World is WalkableWorldControl walkableWorld))
            {
                return;
            }

            if (!DoorPairs.TryGetValue(Type, out int pairedType))
            {
                return;
            }

            var nearbyDoor = walkableWorld.Objects
                .OfType<SteelDoorObject>()
                .FirstOrDefault(door =>
                    door.Type == pairedType &&
                    door._pairedDoor == null);

            if (nearbyDoor != null)
            {
                _pairedDoor = nearbyDoor;
                nearbyDoor._pairedDoor = this;

                RotationDirection = 1;
                nearbyDoor.RotationDirection = -1;
            }
        }

        private void StartRotation(bool open)
        {
            _isRotating = true;
            _rotationTimer = 0f;
            _startAngle = Angle.Z;

            float rotationAmount = MathHelper.PiOver2 * RotationDirection;
            _targetAngle = open ? _startAngle + rotationAmount : _startAngle - rotationAmount;

            if (_pairedDoor != null)
            {
                _pairedDoor.StartRotationInternal(open);
            }
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

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_pairedDoor == null)
            {
                FindPairedDoor();
            }

            Vector2 playerPosition2D = Vector2.Zero;
            if (World is WalkableWorldControl walkableWorld)
            {
                playerPosition2D = walkableWorld.Walker.Location;
            }

            Vector2 thisDoorPosition = new Vector2(Position.X / 100f, Position.Y / 100f);
            float distanceToThisDoor = Vector2.Distance(playerPosition2D, thisDoorPosition);

            float distanceToPairedDoor = float.MaxValue;
            if (_pairedDoor != null)
            {
                Vector2 pairedDoorPosition = new Vector2(_pairedDoor.Position.X / 100f, _pairedDoor.Position.Y / 100f);
                distanceToPairedDoor = Vector2.Distance(playerPosition2D, pairedDoorPosition);
            }

            bool playerInProximity = distanceToThisDoor < ROTATION_PROXIMITY || distanceToPairedDoor < ROTATION_PROXIMITY;

            if (!_isRotating && !_isOpen && playerInProximity)
            {
                StartRotation(true);
            }
            else if (!_isRotating && _isOpen && !playerInProximity)
            {
                StartRotation(false);
            }

            UpdateRotation(gameTime);
            _pairedDoor?.UpdateRotation(gameTime);
        }

        private void UpdateRotation(GameTime gameTime)
        {
            if (_isRotating)
            {
                _rotationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Min(_rotationTimer / ROTATION_DURATION, 1f);

                float smoothProgress = (float)(1 - Math.Cos(progress * Math.PI)) / 2f;
                float currentAngle = MathHelper.Lerp(_startAngle, _targetAngle, smoothProgress);

                Angle = new Vector3(
                    Angle.X,
                    Angle.Y,
                    currentAngle
                );

                if (progress >= 1f)
                {
                    Angle = new Vector3(Angle.X, Angle.Y, _targetAngle);
                    _isRotating = false;
                    _isOpen = !_isOpen;
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
