using Client.Main.Scenes;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Manages static and dynamic lighting for the terrain.
    /// </summary>
    public class TerrainLightManager
    {
        private readonly List<DynamicLight> _dynamicLights = new();
        private readonly List<DynamicLight> _activeLights = new(32);
        private readonly TerrainData _data;
        private readonly GameControl _parent;
        private float _lightUpdateTimer = 0;
        private const float LightUpdateInterval = 0.016f; // Update lights ~60 FPS for smooth pulsing

        // Spatial partitioning for dynamic lights
        private const int LightGridSize = 16; // Grid cells per side (16x16 = 256 cells for 256x256 terrain)
        private const float LightGridCellSize = Constants.TERRAIN_SIZE * Constants.TERRAIN_SCALE / LightGridSize;
        private readonly List<DynamicLight>[,] _lightGrid = new List<DynamicLight>[LightGridSize, LightGridSize];
        
        // Light influence cache
        private readonly Dictionary<int, Vector3> _lightInfluenceCache = new(1024);
        private int _lastLightUpdateFrame = -1;

        public IReadOnlyList<DynamicLight> DynamicLights => _dynamicLights;
        public IReadOnlyList<DynamicLight> ActiveLights => _activeLights;

        public TerrainLightManager(TerrainData data, GameControl parent)
        {
            _data = data;
            _parent = parent;
            
            // Initialize light grid
            for (int y = 0; y < LightGridSize; y++)
            {
                for (int x = 0; x < LightGridSize; x++)
                {
                    _lightGrid[x, y] = new List<DynamicLight>(4);
                }
            }
        }

        public void AddDynamicLight(DynamicLight light) 
        {
            _dynamicLights.Add(light);
            InvalidateLightCache();
        }
        
        public void RemoveDynamicLight(DynamicLight light) 
        {
            _dynamicLights.Remove(light);
            InvalidateLightCache();
        }

        public void CreateTerrainNormals()
        {
            _data.Normals = new Vector3[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int i = GetTerrainIndex(x, y);
                    var v1 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x + 1, y)].B);
                    var v2 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x + 1, y + 1)].B);
                    var v3 = new Vector3(x * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x, y + 1)].B);
                    var v4 = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x, y)].B);

                    var n1 = MathUtils.FaceNormalize(v1, v2, v3);
                    var n2 = MathUtils.FaceNormalize(v3, v4, v1);
                    _data.Normals[i] = n1 + n2;
                }
            }

            foreach (var v in _data.Normals)
                v.Normalize();
        }

        public void CreateFinalLightmap(Vector3 lightDirection)
        {
            _data.FinalLightMap = new Color[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int i = y * Constants.TERRAIN_SIZE + x;
                    float lum = MathHelper.Clamp(Vector3.Dot(_data.Normals[i], lightDirection) + 0.5f, 0f, 1f);
                    _data.FinalLightMap[i] = _data.LightData[i] * lum;
                }
            }
        }

        public void UpdateActiveLights(float deltaTime)
        {
            _lightUpdateTimer += deltaTime;
            if (_lightUpdateTimer < LightUpdateInterval) return;
            _lightUpdateTimer = 0;
            
            _activeLights.Clear();
            if (_dynamicLights.Count == 0 || _parent.World == null) return;

            foreach (var light in _dynamicLights)
            {
                if (light.Intensity <= 0.001f) continue;

                bool isLoginScene = _parent.Scene is LoginScene;
                if (Constants.ENABLE_LOW_QUALITY_SWITCH &&
                    !(isLoginScene && !Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE))
                {
                    var camPos = Camera.Instance.Position;
                    var lightPos = new Vector2(light.Position.X, light.Position.Y);
                    var cam2 = new Vector2(camPos.X, camPos.Y);
                    if (Vector2.DistanceSquared(cam2, lightPos) > Constants.LOW_QUALITY_DISTANCE * Constants.LOW_QUALITY_DISTANCE)
                        continue;
                }

                if (_parent.World.IsLightInView(light))
                    _activeLights.Add(light);
            }

            // Sort lights by distance from player, closest first
            if (_activeLights.Count > 1 && _parent.World is WalkableWorldControl walkable)
            {
                Vector2 playerPos = walkable.Walker.Location;
                Vector3 playerWorldPos = new Vector3(playerPos.X * 100, playerPos.Y * 100, 0);
                
                _activeLights.Sort((light1, light2) =>
                {
                    float dist1 = Vector3.DistanceSquared(light1.Position, playerWorldPos);
                    float dist2 = Vector3.DistanceSquared(light2.Position, playerWorldPos);
                    return dist1.CompareTo(dist2);
                });
            }
            
            // Always rebuild spatial grid when lights are updated (lights can move/change)
            RebuildLightGrid();
            
            // Invalidate cache on light updates to ensure fresh calculations
            InvalidateLightCache();
        }

        public Vector3 EvaluateDynamicLight(Vector2 position)
        {
            // Use time-based caching to avoid repeated calculations within the same frame
            int frameKey = (int)(MuGame.Instance.GameTime.TotalGameTime.TotalMilliseconds / 16.67); // ~60 FPS frame timing
            if (frameKey == _lastLightUpdateFrame)
            {
                int posKey = GetPositionKey(position);
                if (_lightInfluenceCache.TryGetValue(posKey, out Vector3 cachedResult))
                    return cachedResult;
            }
            
            Vector3 result = Vector3.Zero;
            
            // Use spatial partitioning to only check nearby lights
            int gridX = (int)(position.X / LightGridCellSize);
            int gridY = (int)(position.Y / LightGridCellSize);
            
            // Check current cell and adjacent cells (3x3 neighborhood)
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = gridX + dx;
                    int gy = gridY + dy;
                    
                    if (gx < 0 || gx >= LightGridSize || gy < 0 || gy >= LightGridSize)
                        continue;
                        
                    var cellLights = _lightGrid[gx, gy];
                    foreach (var light in cellLights)
                    {
                        if (light.Intensity <= 0.001f) continue;

                        var diff = new Vector2(light.Position.X, light.Position.Y) - position;
                        float distSq = diff.LengthSquared();
                        float radiusSq = light.Radius * light.Radius;
                        if (distSq > radiusSq) continue;

                        // Performance optimization: avoid expensive sqrt by using quadratic falloff
                        float normalizedDistSq = distSq / radiusSq;
                        float factor = 1f - normalizedDistSq; // Quadratic falloff, no sqrt needed
                        result += light.Color * (255f * light.Intensity * factor);
                    }
                }
            }
            
            // Cache the result for this frame
            if (frameKey != _lastLightUpdateFrame)
            {
                _lightInfluenceCache.Clear();
                _lastLightUpdateFrame = frameKey;
            }
            
            if (_lightInfluenceCache.Count < 1000) // Limit cache size
            {
                int posKey = GetPositionKey(position);
                _lightInfluenceCache[posKey] = result;
            }
            
            return result;
        }

        private void RebuildLightGrid()
        {
            // Clear all grid cells
            for (int y = 0; y < LightGridSize; y++)
            {
                for (int x = 0; x < LightGridSize; x++)
                {
                    _lightGrid[x, y].Clear();
                }
            }
            
            // Add active lights to appropriate grid cells (use _activeLights which are already filtered)
            foreach (var light in _activeLights)
            {
                if (light.Intensity > 0.001f) // Double-check intensity
                {
                    AddLightToGrid(light);
                }
            }
        }
        
        private void AddLightToGrid(DynamicLight light)
        {
            // Calculate which grid cells this light affects (based on its radius)
            float radius = light.Radius;
            int minX = Math.Max(0, (int)((light.Position.X - radius) / LightGridCellSize));
            int maxX = Math.Min(LightGridSize - 1, (int)((light.Position.X + radius) / LightGridCellSize));
            int minY = Math.Max(0, (int)((light.Position.Y - radius) / LightGridCellSize));
            int maxY = Math.Min(LightGridSize - 1, (int)((light.Position.Y + radius) / LightGridCellSize));
            
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    _lightGrid[x, y].Add(light);
                }
            }
        }
        
        private static int GetPositionKey(Vector2 position)
        {
            // Create a hash key for position caching (8x8 precision to balance memory vs accuracy)
            int x = (int)(position.X / 64f);
            int y = (int)(position.Y / 64f);
            return (x << 16) | (y & 0xFFFF);
        }
        
        private void InvalidateLightCache()
        {
            _lightInfluenceCache.Clear();
            _lastLightUpdateFrame = -1;
        }

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;

        private static int GetTerrainIndexRepeat(int x, int y)
            => ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE)
             + (x & Constants.TERRAIN_SIZE_MASK);
    }
}
