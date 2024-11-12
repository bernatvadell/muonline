using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class GrassObject : ModelObject
    {
        private float _lastWindUpdate;
        private float _currentAngleX = 0f;
        private float _currentAngleZ = 0f;
        private float _targetAngleX = 0f;
        private float _targetAngleZ = 0f;
        private float _windTime = 0f;
        private Vector2 _windOffset;
        private float _modelHeight = 1.0f;

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

            float terrainHeight = World.Terrain.RequestTerrainHeight(Position.X, Position.Y);

            _modelHeight = WorldPosition.Translation.Z - terrainHeight;
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