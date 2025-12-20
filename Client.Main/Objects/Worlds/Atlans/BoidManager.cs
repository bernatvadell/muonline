using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Client.Main.Objects.Worlds.Atlans
{
    /// <summary>
    /// Manages fish boid behavior in Atlans world.
    /// Based on original client's MoveBoid and MoveBoidGroup algorithms.
    /// </summary>
    public class BoidManager
    {
        private const int MAX_BOIDS = 20; // Maximum number of fish in the area
        private const float NEIGHBOR_RADIUS = 200f; // Distance to consider as neighbor
        private const float SEPARATION_DISTANCE = 80f; // Minimum distance between fish
        private const float COHESION_STRENGTH = 0.15f; // Attraction to group center
        private const float ALIGNMENT_STRENGTH = 0.3f; // Alignment with neighbors
        private const float SEPARATION_STRENGTH = 3.0f; // Repulsion from neighbors
        private const float SPAWN_DISTANCE = 2000f; // Spawn radius around player
        private const int MAX_SPAWN_PER_FRAME = 3; // Spawn 3 fish per frame for faster population
        private const float NORMAL_SPAWN_COOLDOWN = 0.5f; // Spawn every 0.5 seconds

        private List<FishObject> _fishes = new List<FishObject>();
        private Random _random = new Random();
        private WorldControl _world;
        private WalkableWorldControl _walkableWorld;
        private bool _isSpawning = false;
        private float _spawnCooldown = 0f;

        public BoidManager(WorldControl world)
        {
            _world = world;
            _walkableWorld = world as WalkableWorldControl;
        }

        public void Update(GameTime gameTime)
        {
            if (_walkableWorld?.Walker == null)
                return;

            if (_world.Status != GameControlStatus.Ready)
                return;

            Vector3 heroPosition = _walkableWorld.Walker.Position;
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update spawn cooldown
            if (_spawnCooldown > 0f)
            {
                _spawnCooldown -= deltaTime;
            }

            // Spawn new fish if needed
            if (_spawnCooldown <= 0f && !_isSpawning)
            {
                SpawnFish(heroPosition);
                _spawnCooldown = NORMAL_SPAWN_COOLDOWN;
            }

            for (int i = _fishes.Count - 1; i >= 0; i--)
            {
                var fish = _fishes[i];

                if (fish.Status == GameControlStatus.Disposed || fish.Hidden)
                {
                    // Remove dead/hidden fish
                    _world.Objects.Remove(fish);
                    _fishes.RemoveAt(i);
                    continue;
                }

                // Check distance from hero - despawn if too far
                float distanceFromHero = fish.GetDistanceFromHero(heroPosition);
                if (distanceFromHero > 1500f)
                {
                    _world.Objects.Remove(fish);
                    _fishes.RemoveAt(i);
                    fish.Dispose();
                    continue;
                }

                // Check if fish is within valid bounds before accessing terrain
                // Scale world coordinates to terrain grid
                const int TERRAIN_SIZE = 256;
                float terrainScale = Constants.TERRAIN_SCALE;
                int terrainX = (int)(fish.Position.X / terrainScale);
                int terrainY = (int)(fish.Position.Y / terrainScale);

                if (terrainX < 0 || terrainX >= TERRAIN_SIZE ||
                    terrainY < 0 || terrainY >= TERRAIN_SIZE)
                {
                    // Out of bounds - despawn
                    _world.Objects.Remove(fish);
                    _fishes.RemoveAt(i);
                    fish.Dispose();
                    continue;
                }

                // IMPORTANT: Don't update fish here - WorldControl.Update() will call fish.Update()
                // which loads the model and handles the base update logic
                // We only update movement AI here

                // Skip AI updates if fish is not ready yet
                if (fish.Status != GameControlStatus.Ready)
                    continue;

                // Get terrain height at fish position (using world coordinates)
                float terrainHeight = _world.Terrain.RequestTerrainHeight(fish.Position.X, fish.Position.Y);

                // Update fish movement and environment interaction
                fish.UpdateMovement(gameTime, heroPosition, terrainHeight);

                // Apply boid algorithm
                ApplyBoidBehavior(fish, i, gameTime);

                // Apply calculated movement
                fish.ApplyMovement(gameTime);

                // Check boundaries and turn around if needed
                CheckBoundaries(fish, terrainHeight);
            }
        }

        /// <summary>
        /// Spawns fish around the player when underwater (Y &lt; 128 in scaled coords).
        /// In original MU: Position[1] is Y axis (depth), Position[2] is Z axis (height).
        /// Spawns gradually to avoid blocking the main thread
        /// </summary>
        private void SpawnFish(Vector3 heroPosition)
        {
            if (_fishes.Count >= MAX_BOIDS)
                return;

            // Already spawning, skip this frame
            if (_isSpawning)
                return;

            _isSpawning = true;

            // Spawn only a few fish per frame to avoid blocking
            int spawnCount = 0;
            int targetSpawnPerFrame = MAX_SPAWN_PER_FRAME;
            int maxAttempts = targetSpawnPerFrame * 5; // Try a few times to find valid positions
            int attempts = 0;

            while (spawnCount < targetSpawnPerFrame && _fishes.Count < MAX_BOIDS && attempts < maxAttempts)
            {
                attempts++;

                // Spawn in a larger area around player (up to SPAWN_DISTANCE)
                // Use circular distribution for more natural spawning
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                float distance = (float)(_random.NextDouble() * SPAWN_DISTANCE * 0.5f) + SPAWN_DISTANCE * 0.5f; // 50-100% of spawn distance
                float offsetX = MathF.Cos(angle) * distance;
                float offsetY = MathF.Sin(angle) * distance;
                Vector3 spawnPos = new Vector3(
                    heroPosition.X + offsetX,  // X coordinate
                    heroPosition.Y + offsetY,  // Y coordinate (depth)
                    heroPosition.Z);           // Z will be set from terrain height

                // IMPORTANT: Terrain uses SCALED coordinates!
                // Original code: RequestTerrainHeight(o->Position[0], o->Position[1])
                // Position is in world units, terrain grid is 256x256
                // We need to scale world position to terrain grid
                const int TERRAIN_SIZE = 256;

                // Scale world coordinates to terrain grid (divide by terrain scale)
                float terrainScale = Constants.TERRAIN_SCALE; // Usually 100
                int spawnX = (int)(spawnPos.X / terrainScale);
                int spawnY = (int)(spawnPos.Y / terrainScale);

                // Skip this spawn if out of bounds
                if (spawnX < 0 || spawnX >= TERRAIN_SIZE ||
                    spawnY < 0 || spawnY >= TERRAIN_SIZE)
                {
                    continue;
                }

                // Check terrain flags before spawning - avoid NoMove, Height, and SafeZone
                var spawnTerrainFlag = _world.Terrain.RequestTerrainFlag(spawnX, spawnY);
                if (spawnTerrainFlag.HasFlag(Client.Data.ATT.TWFlags.NoMove) ||
                    spawnTerrainFlag.HasFlag(Client.Data.ATT.TWFlags.Height) ||
                    spawnTerrainFlag.HasFlag(Client.Data.ATT.TWFlags.SafeZone))
                {
                    continue; // Skip this spawn location
                }

                // Get terrain height at XY position (using world coordinates)
                // Original: o->Position[2] = RequestTerrainHeight(o->Position[0], o->Position[1]) + (float)(rand() % 200 + 150);
                // RequestTerrainHeight expects world coordinates, not grid coordinates
                float terrainHeight = _world.Terrain.RequestTerrainHeight(spawnPos.X, spawnPos.Y);

                // Set initial spawn height to fixed swim height
                const float INITIAL_SWIM_HEIGHT = 80f;
                spawnPos.Z = terrainHeight + INITIAL_SWIM_HEIGHT;

                // Create fish object
                // NOTE: Don't set World property manually - it's set automatically when adding to World.Objects

                // Initialize random direction using MU Online's Direction system
                var randomDirections = new[] {
                    Direction.West, Direction.SouthWest, Direction.South, Direction.SouthEast,
                    Direction.East, Direction.NorthEast, Direction.North, Direction.NorthWest
                };
                var randomDir = randomDirections[_random.Next(randomDirections.Length)];
                Vector3 initialAngle = randomDir.ToAngle();

                // Calculate initial velocity direction from the isometric angle
                float angleRad = initialAngle.Z;
                Vector3 initialDirection = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);

                // Random scale variation: 1.7 to 2.3 (base is 2.0)
                float randomScale = 1.7f + (float)_random.NextDouble() * 0.6f;

                var fish = new FishObject
                {
                    Type = (short)(_random.Next(0, 2) + 1), // Fish type 1 or 2 (MODEL_FISH01+2 or +3)
                    Position = spawnPos,
                    Scale = randomScale, // Random size variation
                    // Start facing the initial direction (isometric angle)
                    Angle = initialAngle,
                    // Initialize Direction immediately so ApplyMovement has correct target
                    Direction = initialDirection
                };

                _fishes.Add(fish);

                // Add to world - this will automatically set fish.World and Load() will be called in Update()
                _world.Objects.Add(fish);

                spawnCount++;
            }

            _isSpawning = false;
        }

        /// <summary>
        /// Applies Boid algorithm (separation, alignment, cohesion) to a fish.
        /// NEW: Works with velocity-based system - modifies desired direction only.
        /// </summary>
         private void ApplyBoidBehavior(FishObject fish, int fishIndex, GameTime gameTime)
        {
            Vector3 separation = Vector3.Zero;
            Vector3 alignment = Vector3.Zero;
            Vector3 cohesion = Vector3.Zero;
            int neighborCount = 0;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Find neighbors and calculate boid forces
            for (int i = 0; i < _fishes.Count; i++)
            {
                if (i == fishIndex)
                    continue;

                var other = _fishes[i];
                float distance = Vector3.Distance(fish.Position, other.Position);

                // Only consider nearby fish as neighbors
                if (distance < NEIGHBOR_RADIUS && distance > 0.1f)
                {
                    neighborCount++;

                    // Separation: steer away from nearby fish
                    if (distance < SEPARATION_DISTANCE)
                    {
                        Vector3 away = fish.Position - other.Position;
                        if (away.LengthSquared() > 0.001f)
                        {
                            away.Normalize();
                            separation += away / distance; // Stronger when closer
                        }
                    }

                    // Alignment: match direction of neighbors
                    alignment += other.Direction;

                    // Cohesion: move towards center of neighbors
                    cohesion += other.Position;
                }
            }

            if (neighborCount > 0)
            {
                // Average the forces
                alignment /= neighborCount;
                cohesion /= neighborCount;

                // Cohesion: steer towards center of mass
                cohesion = cohesion - fish.Position;
                if (cohesion.LengthSquared() > 0.001f)
                {
                    cohesion.Normalize();
                }

                // Apply forces to desired direction
                // fish.Direction setter automatically normalizes, so we can just add influences
                Vector3 currentDir = fish.Direction;
                Vector3 boidInfluence =
                    separation * SEPARATION_STRENGTH * deltaTime +
                    alignment * ALIGNMENT_STRENGTH * deltaTime +
                    cohesion * COHESION_STRENGTH * deltaTime;

                fish.Direction = currentDir + boidInfluence; // Setter will normalize
            }
        }

        /// <summary>
        /// Checks terrain boundaries and turns fish around if needed
        /// Based on original wall detection logic
        /// Fish avoid NoMove, Height, and SafeZone areas
        /// </summary>
        private void CheckBoundaries(FishObject fish, float terrainHeight)
        {
            // Check if fish is within valid terrain bounds
            // Scale world coordinates to terrain grid
            const int TERRAIN_SIZE = 256;
            float terrainScale = Constants.TERRAIN_SCALE;
            int terrainX = (int)(fish.Position.X / terrainScale);
            int terrainY = (int)(fish.Position.Y / terrainScale);

            // Check if out of bounds - if so, despawn immediately
            if (terrainX < 0 || terrainX >= TERRAIN_SIZE ||
                terrainY < 0 || terrainY >= TERRAIN_SIZE)
            {
                fish.Hidden = true;
                return;
            }

            // Get terrain flags at fish position (using grid coordinates)
            var terrainFlag = _world.Terrain.RequestTerrainFlag(terrainX, terrainY);

            // Check if fish hit a wall, impassable terrain, or safe zone
            bool hitObstacle = terrainFlag.HasFlag(Client.Data.ATT.TWFlags.NoMove) ||
                              terrainFlag.HasFlag(Client.Data.ATT.TWFlags.Height) ||
                              terrainFlag.HasFlag(Client.Data.ATT.TWFlags.SafeZone);

            if (hitObstacle)
            {
                // Turn around 180 degrees
                fish.Angle = new Vector3(fish.Angle.X, fish.Angle.Y, fish.Angle.Z + 180f);
                if (fish.Angle.Z >= 360f)
                    fish.Angle = new Vector3(fish.Angle.X, fish.Angle.Y, fish.Angle.Z - 360f);

                // Update desired direction to match new angle
                float angleRad = fish.Angle.Z;
                fish.Direction = new Vector3(MathF.Cos(angleRad), MathF.Sin(angleRad), 0f);

                fish.SubType++;

                // Despawn if turned around too many times
                if (fish.SubType >= 2)
                {
                    fish.Hidden = true;
                }
            }
            else
            {
                // Reset turn counter if swimming freely
                if (fish.SubType > 0)
                    fish.SubType--;
            }
        }

        /// <summary>
        /// Clears all fish from the world
        /// </summary>
        public void Clear()
        {
            foreach (var fish in _fishes)
            {
                _world.Objects.Remove(fish);
                fish.Dispose();
            }
            _fishes.Clear();
        }

        public int FishCount => _fishes.Count;
    }
}
