using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class GrassObject : ModelObject
    {
        // Wind deformation updates world transform every frame; keep classic per-object draw path.
        protected override bool AllowMapObjectInstancing => false;

        private float _lastWindUpdate;
        private float _currentAngleX = 0f;
        private float _currentAngleZ = 0f;
        private float _targetAngleX = 0f;
        private float _targetAngleZ = 0f;
        private float _windTime = 0f;
        private Vector2 _windOffset;
        private float _modelHeight = 1.0f;
        private const float TERRAIN_OFFSET = -10f;

        private const float WIND_UPDATE_INTERVAL = 32f;
        private const float BASE_WIND_INTENSITY = 0.015f;
        private const float WIND_SMOOTH_SPEED = 0.2f;
        private const float WIND_WAVE_SPEED = 0.2f;
        private const float MAX_ANGLE = 0.25f;
        private const float RANDOM_INTENSITY = 0.25f;
        private const float HEIGHT_INFLUENCE = 1.0f;
        private const float HEIGHT_GRADIENT = 0.5f;

        private readonly Random _random;

        public GrassObject()
        {
            LightEnabled = true;
            _random = new Random(GetHashCode());
            _windOffset = new Vector2(
                (float)_random.NextDouble() * MathHelper.TwoPi,
                (float)_random.NextDouble() * MathHelper.TwoPi
            );
        }

        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.Grass01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Grass{idx}.bmd");
            await base.Load();

            // Calculate model height and terrain height
            _modelHeight = (BoundingBoxLocal.Max.Z - BoundingBoxLocal.Min.Z) * TotalScale;
            float terrainHeight = World.Terrain.RequestTerrainHeight(Position.X, Position.Y);

            if (Position.Z < terrainHeight)
            {
                return;
            }

            // Adjust position based on model type and terrain
            if (Type == 25)
            {
                // For these types, ensure they're properly grounded with slight elevation
                float baseHeight = terrainHeight + TERRAIN_OFFSET;
                Position = new Vector3(Position.X, Position.Y, baseHeight);
            }
            else if (Type == 24 || Type == 23 || Type == 22)
            {
                Position = new Vector3(
                    Position.X,
                    Position.Y - 80f,
                    terrainHeight + TERRAIN_OFFSET
                );
            }
            else
            {
                Position = new Vector3(
                    Position.X,
                    Position.Y,
                    Position.Z + TERRAIN_OFFSET
                );
            }

            // Additional height adjustment for ground cover types
            if (IsGroundCoverType())
            {
                AdjustGroundCoverHeight(terrainHeight);
            }
        }

        private bool IsGroundCoverType()
        {
            return Type == 22 || Type == 23 || Type == 24 || Type == 25;
        }

        private void AdjustGroundCoverHeight(float terrainHeight)
        {
            // For ground cover, we want to ensure it follows terrain contours
            // Calculate the actual bottom point of the model in world space
            float modelBottom = Position.Z - (_modelHeight * 0.5f); // Assuming model origin is at center

            // If the model bottom is below terrain, raise it
            if (modelBottom < terrainHeight)
            {
                float adjustment = terrainHeight - modelBottom + TERRAIN_OFFSET;
                Position = new Vector3(Position.X, Position.Y, Position.Z + adjustment);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _windTime += deltaTime * WIND_WAVE_SPEED;

            if (gameTime.TotalGameTime.TotalMilliseconds - _lastWindUpdate >= WIND_UPDATE_INTERVAL)
            {
                _lastWindUpdate = (float)gameTime.TotalGameTime.TotalMilliseconds;
                UpdateWindTarget(deltaTime);
            }

            float smoothFactor = deltaTime * WIND_SMOOTH_SPEED;
            _currentAngleX = MathHelper.Lerp(_currentAngleX, _targetAngleX, smoothFactor);
            _currentAngleZ = MathHelper.Lerp(_currentAngleZ, _targetAngleZ, smoothFactor);

            ApplyWindEffect();
        }

        private void UpdateWindTarget(float deltaTime)
        {
            var worldControl = World;
            if (worldControl?.Terrain == null) return;

            int terrainX = (int)(Position.X / Constants.TERRAIN_SCALE);
            int terrainY = (int)(Position.Y / Constants.TERRAIN_SCALE);

            float baseWind = worldControl.Terrain.GetWindValue(terrainX, terrainY) * BASE_WIND_INTENSITY;
            float windDirection = _windTime * 0.5f;

            float waveX = MathF.Sin(_windTime + _windOffset.X);
            float waveZ = MathF.Sin(_windTime + _windOffset.Y + MathF.PI * 0.25f);

            float randomX = ((float)_random.NextDouble() * 2 - 1) * RANDOM_INTENSITY * deltaTime;
            float randomZ = ((float)_random.NextDouble() * 2 - 1) * RANDOM_INTENSITY * deltaTime;

            _targetAngleX = (baseWind * MathF.Cos(windDirection) + waveX * BASE_WIND_INTENSITY + randomX) * HEIGHT_INFLUENCE;
            _targetAngleZ = (baseWind * MathF.Sin(windDirection) + waveZ * BASE_WIND_INTENSITY + randomZ) * HEIGHT_INFLUENCE;

            _targetAngleX = MathHelper.Clamp(_targetAngleX, -MAX_ANGLE, MAX_ANGLE);
            _targetAngleZ = MathHelper.Clamp(_targetAngleZ, -MAX_ANGLE, MAX_ANGLE);
        }

        private void ApplyWindEffect()
        {
            var position = Position;
            float effectiveHeight = _modelHeight * Scale;

            Matrix finalTransform = Matrix.Identity;
            const int sections = 5;

            for (int i = 0; i < sections; i++)
            {
                float heightFactor = (float)i / (sections - 1);
                float sectionInfluence = MathF.Pow(heightFactor, HEIGHT_GRADIENT);

                float sectionAngleX = _currentAngleX * sectionInfluence;
                float sectionAngleZ = _currentAngleZ * sectionInfluence;

                Matrix sectionTransform = Matrix.CreateScale(Scale) *
                                        Matrix.CreateRotationX(sectionAngleX) *
                                        Matrix.CreateRotationZ(sectionAngleZ);

                float sectionHeight = effectiveHeight * heightFactor;
                sectionTransform *= Matrix.CreateTranslation(new Vector3(position.X, position.Y + sectionHeight, position.Z));

                float blendFactor = (i == sections - 1) ? 1.0f : (1.0f / (sections - i));
                finalTransform = Matrix.Lerp(finalTransform, sectionTransform, blendFactor);
            }

            if (Parent != null)
            {
                finalTransform *= Parent.WorldPosition;
            }

            WorldPosition = finalTransform;
        }

        protected override void RecalculateWorldPosition()
        {
            base.RecalculateWorldPosition();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
