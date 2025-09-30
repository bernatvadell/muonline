using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Client.Main.Objects.Worlds.Lorencia
{
    /// <summary>
    /// Manages bird spawning, movement, and despawning in Lorencia world.
    /// Birds fly in loose flocks with less cohesion than fish.
    /// </summary>
    public class BirdManager
    {
        private const int MAX_BIRDS = 20; // More birds than butterflies
        private const float NEIGHBOR_RADIUS = 200f; // Distance to consider as neighbor
        private const float SEPARATION_DISTANCE = 180f; // Minimum distance between birds (increased to prevent mesh overlap)
        private const float COHESION_STRENGTH = 0.08f; // Weak attraction to group
        private const float ALIGNMENT_STRENGTH = 0.15f; // Weak alignment (more than butterflies)
        private const float SEPARATION_STRENGTH = 3.0f; // Strong repulsion to prevent overlap
        private const float SPAWN_DISTANCE = 2500f; // Spawn radius around player
        private const int MAX_SPAWN_PER_FRAME = 3; // Spawn 3 birds per frame
        private const float NORMAL_SPAWN_COOLDOWN = 0.6f; // Spawn every 0.6 seconds

        private List<BirdObject> _birds = new List<BirdObject>();
        private WalkableWorldControl _world;
        private Random _random = new Random();
        private float _spawnCooldown = 0f;
        private bool _isSpawning = false;

        public BirdManager(WalkableWorldControl world)
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

            // Spawn birds if needed
            if (_birds.Count < MAX_BIRDS && _spawnCooldown <= 0f && !_isSpawning)
            {
                SpawnBirds(heroPosition);
                _spawnCooldown = NORMAL_SPAWN_COOLDOWN;
            }

            // Update all birds
            for (int i = _birds.Count - 1; i >= 0; i--)
            {
                var bird = _birds[i];

                // Check if bird is too far from hero - despawn
                float distanceFromHero = bird.GetDistanceFromHero(heroPosition);
                if (distanceFromHero > 2000f)
                {
                    _world.Objects.Remove(bird);
                    _birds.RemoveAt(i);
                    bird.Dispose();
                    continue;
                }

                // Update movement behavior
                bird.UpdateMovement(gameTime, heroPosition);

                // Apply weak boid behavior (only when flying, not when landing/taking off)
                if (bird.AIState == BirdObject.BirdAIState.Flying)
                {
                    ApplyBoidBehavior(bird, i, gameTime);
                }

                // Check boundaries (walls, safe zones)
                CheckBoundaries(bird);

                // Apply final movement
                bird.ApplyMovement(gameTime);
            }
        }

        /// <summary>
        /// Spawns birds in circular pattern around player at flying height
        /// </summary>
        private void SpawnBirds(Vector3 heroPosition)
        {
            _isSpawning = true;

            int spawnCount = 0;
            int maxAttempts = MAX_SPAWN_PER_FRAME * 5;

            for (int attempt = 0; attempt < maxAttempts && spawnCount < MAX_SPAWN_PER_FRAME; attempt++)
            {
                if (_birds.Count >= MAX_BIRDS)
                    break;

                // Circular spawn distribution around hero
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                float distance = (float)(_random.NextDouble() * SPAWN_DISTANCE * 0.5f) + SPAWN_DISTANCE * 0.5f;
                float offsetX = MathF.Cos(angle) * distance;
                float offsetY = MathF.Sin(angle) * distance;

                Vector3 spawnPos = new Vector3(
                    heroPosition.X + offsetX,
                    heroPosition.Y + offsetY,
                    0f
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

                // Spawn at flying height (200-600 above terrain)
                float spawnHeight = terrainHeight + 200f + (float)(_random.NextDouble() * 400f);

                spawnPos = new Vector3(spawnPos.X, spawnPos.Y, spawnHeight);

                // Random initial angle
                float randomAngle = (float)(_random.NextDouble() * MathF.PI * 2f);
                Vector3 initialAngle = new Vector3(0f, 0f, randomAngle);

                // Calculate initial direction from angle
                float angleRad = initialAngle.Z;
                Vector3 initialDirection = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);

                // Random scale variation: 0.75 to 1.0 (smaller birds)
                float randomScale = 0.75f + (float)_random.NextDouble() * 0.25f;

                var bird = new BirdObject
                {
                    Position = spawnPos,
                    Scale = randomScale,
                    Angle = initialAngle,
                    Direction = initialDirection
                };

                _birds.Add(bird);
                _world.Objects.Add(bird);

                spawnCount++;
            }

            _isSpawning = false;
        }

        /// <summary>
        /// Applies weak boid behavior - birds fly in loose flocks
        /// </summary>
        private void ApplyBoidBehavior(BirdObject bird, int birdIndex, GameTime gameTime)
        {
            Vector3 separation = Vector3.Zero;
            Vector3 alignment = Vector3.Zero;
            Vector3 cohesion = Vector3.Zero;
            int neighborCount = 0;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Find neighbors (only other flying birds)
            for (int i = 0; i < _birds.Count; i++)
            {
                if (i == birdIndex)
                    continue;

                var other = _birds[i];

                // Only flock with other flying birds
                if (other.AIState != BirdObject.BirdAIState.Flying)
                    continue;

                float distance = Vector3.Distance(bird.Position, other.Position);

                if (distance < NEIGHBOR_RADIUS && distance > 0.1f)
                {
                    neighborCount++;

                    // Separation: avoid collisions
                    if (distance < SEPARATION_DISTANCE)
                    {
                        Vector3 away = bird.Position - other.Position;
                        if (away.LengthSquared() > 0.001f)
                        {
                            away.Normalize();
                            separation += away / distance;
                        }
                    }

                    // Alignment
                    alignment += other.Direction;

                    // Cohesion
                    cohesion += other.Position;
                }
            }

            if (neighborCount > 0)
            {
                alignment /= neighborCount;
                cohesion /= neighborCount;

                cohesion = cohesion - bird.Position;
                if (cohesion.LengthSquared() > 0.001f)
                {
                    cohesion.Normalize();
                }

                // Apply weak forces (birds have weak flocking)
                Vector3 boidInfluence =
                    separation * SEPARATION_STRENGTH +
                    alignment * ALIGNMENT_STRENGTH +
                    cohesion * COHESION_STRENGTH;

                boidInfluence *= deltaTime;

                Vector3 currentDir = bird.Direction;
                if (currentDir.LengthSquared() < 0.001f)
                    currentDir = Vector3.UnitX;

                bird.Direction = currentDir + boidInfluence;
            }
        }

        /// <summary>
        /// Checks terrain boundaries and turns bird around if needed
        /// Birds avoid NoMove, Height, and SafeZone areas
        /// </summary>
        private void CheckBoundaries(BirdObject bird)
        {
            const int TERRAIN_SIZE = 256;
            float terrainScale = Constants.TERRAIN_SCALE;
            int terrainX = (int)(bird.Position.X / terrainScale);
            int terrainY = (int)(bird.Position.Y / terrainScale);

            // Check if out of bounds
            if (terrainX < 0 || terrainX >= TERRAIN_SIZE ||
                terrainY < 0 || terrainY >= TERRAIN_SIZE)
            {
                bird.Hidden = true;
                return;
            }

            // Get terrain flags
            var terrainFlag = _world.Terrain.RequestTerrainFlag(terrainX, terrainY);

            // Check for obstacles or safe zones
            bool hitObstacle = terrainFlag.HasFlag(Client.Data.ATT.TWFlags.NoMove) ||
                              terrainFlag.HasFlag(Client.Data.ATT.TWFlags.SafeZone);

            if (hitObstacle)
            {
                // Turn around 180 degrees
                bird.Angle = new Vector3(bird.Angle.X, bird.Angle.Y, bird.Angle.Z + MathF.PI);
                if (bird.Angle.Z >= MathHelper.TwoPi)
                    bird.Angle = new Vector3(bird.Angle.X, bird.Angle.Y, bird.Angle.Z - MathHelper.TwoPi);

                // Update direction to match new angle
                float angleRad = bird.Angle.Z;
                bird.Direction = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);

                bird.SubType++;

                // Despawn if turned around too many times
                if (bird.SubType >= 3)
                {
                    bird.Hidden = true;
                }
            }
            else
            {
                // Reset turn counter
                if (bird.SubType > 0)
                    bird.SubType--;
            }
        }

        /// <summary>
        /// Clears all birds from the world
        /// </summary>
        public void Clear()
        {
            foreach (var bird in _birds)
            {
                _world.Objects.Remove(bird);
                bird.Dispose();
            }
            _birds.Clear();
        }
    }
}
