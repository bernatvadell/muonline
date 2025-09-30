using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Client.Main.Objects.Worlds.Noria
{
    /// <summary>
    /// Manages butterfly spawning, movement, and despawning in Noria world.
    /// Butterflies fly more independently than fish with less flocking behavior.
    /// </summary>
    public class ButterflyManager
    {
        private const int MAX_BUTTERFLIES = 15; // Maximum number of butterflies
        private const float NEIGHBOR_RADIUS = 150f; // Distance to consider as neighbor
        private const float SEPARATION_DISTANCE = 100f; // Minimum distance between butterflies
        private const float COHESION_STRENGTH = 0.01f; // Almost no attraction to group (reduced from 0.05)
        private const float ALIGNMENT_STRENGTH = 0.02f; // Almost no alignment (reduced from 0.1)
        private const float SEPARATION_STRENGTH = 2.5f; // Stronger repulsion to avoid collisions
        private const float SPAWN_DISTANCE = 2000f; // Spawn radius around player
        private const int MAX_SPAWN_PER_FRAME = 2; // Spawn 2 butterflies per frame
        private const float NORMAL_SPAWN_COOLDOWN = 0.8f; // Spawn every 0.8 seconds

        private List<ButterflyObject> _butterflies = new List<ButterflyObject>();
        private WalkableWorldControl _world;
        private Random _random = new Random();
        private float _spawnCooldown = 0f;
        private bool _isSpawning = false;

        public ButterflyManager(WalkableWorldControl world)
        {
            _world = world;
        }

        public void Update(GameTime gameTime)
        {
            if (_world?.Walker == null)
                return;

            Vector3 heroPosition = _world.Walker.Position;
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update spawn cooldown
            _spawnCooldown -= deltaTime;

            // Spawn butterflies if needed
            if (_butterflies.Count < MAX_BUTTERFLIES && _spawnCooldown <= 0f && !_isSpawning)
            {
                SpawnButterflies(heroPosition);
                _spawnCooldown = NORMAL_SPAWN_COOLDOWN;
            }

            // Update all butterflies
            for (int i = _butterflies.Count - 1; i >= 0; i--)
            {
                var butterfly = _butterflies[i];

                // Check if butterfly is too far from hero - despawn
                float distanceFromHero = butterfly.GetDistanceFromHero(heroPosition);
                if (distanceFromHero > 1500f)
                {
                    _world.Objects.Remove(butterfly);
                    _butterflies.RemoveAt(i);
                    butterfly.Dispose();
                    continue;
                }

                // Get terrain height at butterfly position
                float terrainHeight = _world.Terrain.RequestTerrainHeight(butterfly.Position.X, butterfly.Position.Y);

                // Update movement behavior
                butterfly.UpdateMovement(gameTime, heroPosition, terrainHeight);

                // Apply weak boid behavior (butterflies are less social than fish)
                ApplyBoidBehavior(butterfly, i, gameTime);

                // Check boundaries (walls, safe zones)
                CheckBoundaries(butterfly, terrainHeight);

                // Apply final movement
                butterfly.ApplyMovement(gameTime, terrainHeight);
            }
        }

        /// <summary>
        /// Spawns butterflies in circular pattern around player
        /// </summary>
        private void SpawnButterflies(Vector3 heroPosition)
        {
            _isSpawning = true;

            int spawnCount = 0;
            int maxAttempts = MAX_SPAWN_PER_FRAME * 5; // Try 5 times per butterfly

            for (int attempt = 0; attempt < maxAttempts && spawnCount < MAX_SPAWN_PER_FRAME; attempt++)
            {
                if (_butterflies.Count >= MAX_BUTTERFLIES)
                    break;

                // Circular spawn distribution around hero
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                float distance = (float)(_random.NextDouble() * SPAWN_DISTANCE * 0.5f) + SPAWN_DISTANCE * 0.5f;
                float offsetX = MathF.Cos(angle) * distance;
                float offsetY = MathF.Sin(angle) * distance;

                Vector3 spawnPos = new Vector3(
                    heroPosition.X + offsetX,
                    heroPosition.Y + offsetY,
                    0f // Z will be set based on terrain
                );

                // Validate spawn position is within terrain bounds
                const int TERRAIN_SIZE = 256;
                float terrainScale = Constants.TERRAIN_SCALE;
                int spawnX = (int)(spawnPos.X / terrainScale);
                int spawnY = (int)(spawnPos.Y / terrainScale);

                // Skip if out of bounds
                if (spawnX < 0 || spawnX >= TERRAIN_SIZE ||
                    spawnY < 0 || spawnY >= TERRAIN_SIZE)
                {
                    continue;
                }

                // Check terrain flags - avoid NoMove, Height, and SafeZone
                var spawnTerrainFlag = _world.Terrain.RequestTerrainFlag(spawnX, spawnY);
                if (spawnTerrainFlag.HasFlag(Client.Data.ATT.TWFlags.NoMove) ||
                    spawnTerrainFlag.HasFlag(Client.Data.ATT.TWFlags.Height) ||
                    spawnTerrainFlag.HasFlag(Client.Data.ATT.TWFlags.SafeZone))
                {
                    continue;
                }

                // Get terrain height
                float terrainHeight = _world.Terrain.RequestTerrainHeight(spawnPos.X, spawnPos.Y);

                // Random initial height above terrain (50-300)
                float initialHeight = terrainHeight + 50f + (float)(_random.NextDouble() * 250f);

                spawnPos = new Vector3(spawnPos.X, spawnPos.Y, initialHeight);

                // Random initial angle
                float randomAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 initialAngle = new Vector3(0f, 0f, randomAngle);

                // Calculate initial direction from angle
                float angleRad = initialAngle.Z;
                Vector3 initialDirection = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);

                // Random scale variation: 0.8 to 1.2
                float randomScale = 0.7f + (float)_random.NextDouble() * 0.4f;

                var butterfly = new ButterflyObject
                {
                    Position = spawnPos,
                    Scale = randomScale,
                    Angle = initialAngle,
                    Direction = initialDirection
                };

                _butterflies.Add(butterfly);
                _world.Objects.Add(butterfly);

                spawnCount++;
            }

            _isSpawning = false;
        }

        /// <summary>
        /// Applies weak boid behavior - butterflies are more independent than fish
        /// </summary>
        private void ApplyBoidBehavior(ButterflyObject butterfly, int butterflyIndex, GameTime gameTime)
        {
            Vector3 separation = Vector3.Zero;
            Vector3 alignment = Vector3.Zero;
            Vector3 cohesion = Vector3.Zero;
            int neighborCount = 0;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Find neighbors
            for (int i = 0; i < _butterflies.Count; i++)
            {
                if (i == butterflyIndex)
                    continue;

                var other = _butterflies[i];
                float distance = Vector3.Distance(butterfly.Position, other.Position);

                if (distance < NEIGHBOR_RADIUS && distance > 0.1f)
                {
                    neighborCount++;

                    // Separation: avoid collisions
                    if (distance < SEPARATION_DISTANCE)
                    {
                        Vector3 away = butterfly.Position - other.Position;
                        if (away.LengthSquared() > 0.001f)
                        {
                            away.Normalize();
                            separation += away / distance;
                        }
                    }

                    // Weak alignment
                    alignment += other.Direction;

                    // Weak cohesion
                    cohesion += other.Position;
                }
            }

            if (neighborCount > 0)
            {
                alignment /= neighborCount;
                cohesion /= neighborCount;

                cohesion = cohesion - butterfly.Position;
                if (cohesion.LengthSquared() > 0.001f)
                {
                    cohesion.Normalize();
                }

                // Apply very weak forces (butterflies are independent)
                Vector3 boidInfluence =
                    separation * SEPARATION_STRENGTH +
                    alignment * ALIGNMENT_STRENGTH +
                    cohesion * COHESION_STRENGTH;

                boidInfluence *= deltaTime;

                Vector3 currentDir = butterfly.Direction;
                if (currentDir.LengthSquared() < 0.001f)
                    currentDir = Vector3.UnitX;

                butterfly.Direction = currentDir + boidInfluence;
            }
        }

        /// <summary>
        /// Checks terrain boundaries and turns butterfly around if needed
        /// Butterflies avoid NoMove, Height, and SafeZone areas
        /// </summary>
        private void CheckBoundaries(ButterflyObject butterfly, float terrainHeight)
        {
            const int TERRAIN_SIZE = 256;
            float terrainScale = Constants.TERRAIN_SCALE;
            int terrainX = (int)(butterfly.Position.X / terrainScale);
            int terrainY = (int)(butterfly.Position.Y / terrainScale);

            // Check if out of bounds
            if (terrainX < 0 || terrainX >= TERRAIN_SIZE ||
                terrainY < 0 || terrainY >= TERRAIN_SIZE)
            {
                butterfly.Hidden = true;
                return;
            }

            // Get terrain flags
            var terrainFlag = _world.Terrain.RequestTerrainFlag(terrainX, terrainY);

            // Check for obstacles or safe zones
            bool hitObstacle = terrainFlag.HasFlag(Client.Data.ATT.TWFlags.NoMove) ||
                              terrainFlag.HasFlag(Client.Data.ATT.TWFlags.Height) ||
                              terrainFlag.HasFlag(Client.Data.ATT.TWFlags.SafeZone);

            if (hitObstacle)
            {
                // Turn around 180 degrees
                butterfly.Angle = new Vector3(butterfly.Angle.X, butterfly.Angle.Y, butterfly.Angle.Z + MathF.PI);
                if (butterfly.Angle.Z >= MathHelper.TwoPi)
                    butterfly.Angle = new Vector3(butterfly.Angle.X, butterfly.Angle.Y, butterfly.Angle.Z - MathHelper.TwoPi);

                // Update direction to match new angle
                float angleRad = butterfly.Angle.Z;
                butterfly.Direction = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);

                butterfly.SubType++;

                // Despawn if turned around too many times
                if (butterfly.SubType >= 3)
                {
                    butterfly.Hidden = true;
                }
            }
            else
            {
                // Reset turn counter
                if (butterfly.SubType > 0)
                    butterfly.SubType--;
            }
        }

        /// <summary>
        /// Clears all butterflies from the world
        /// </summary>
        public void Clear()
        {
            foreach (var butterfly in _butterflies)
            {
                _world.Objects.Remove(butterfly);
                butterfly.Dispose();
            }
            _butterflies.Clear();
        }
    }
}
