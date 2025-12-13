using Client.Main.Scenes;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Manages static and dynamic lighting for the terrain.
    /// </summary>
    public class TerrainLightManager
    {
        private struct PrecomputedLight
        {
            public Vector2 Position;
            public Vector3 ColorScaled;
            public float Radius;
            public float RadiusSq;
            public float InvRadiusSq;
        }

        private readonly List<DynamicLight> _dynamicLights = new();
        private readonly List<DynamicLightSnapshot> _activeLights = new(32);
        private readonly List<PrecomputedLight> _precomputedActiveLights = new(32);
        private readonly TerrainData _data;
        private readonly GameControl _parent;
        private float _lightUpdateTimer = 0;
        private int _activeLightsVersion = 0;
        private const int MaxLightCacheEntries = 4096;
        private const float CacheQuantization = 8f; // Smaller cell to avoid cross-tile flicker
        private const float InvCacheQuantization = 1f / CacheQuantization;

        // Spatial partitioning for dynamic lights
        private const int LightGridSize = 16; // Grid cells per side (16x16 = 256 cells for 256x256 terrain)
        private const float LightGridCellSize = Constants.TERRAIN_SIZE * Constants.TERRAIN_SCALE / LightGridSize;
        private readonly List<int>[,] _lightGrid = new List<int>[LightGridSize, LightGridSize];
        private readonly int[,] _lightGridVersion = new int[LightGridSize, LightGridSize];
        private int _currentLightGridVersion = 0;

        // Per-frame influence cache to avoid recalculating the same sample positions
        private readonly Dictionary<long, Vector3> _lightInfluenceCache = new(MaxLightCacheEntries);
        private int _lightInfluenceCacheVersion = -1;

        public IReadOnlyList<DynamicLight> DynamicLights => _dynamicLights;
        public IReadOnlyList<DynamicLightSnapshot> ActiveLights => _activeLights;
        public int ActiveLightsVersion => _activeLightsVersion;

        public TerrainLightManager(TerrainData data, GameControl parent)
        {
            _data = data;
            _parent = parent;

            // Initialize light grid
            for (int y = 0; y < LightGridSize; y++)
            {
                for (int x = 0; x < LightGridSize; x++)
                {
                    _lightGrid[x, y] = new List<int>(4);
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
                        _data.HeightMap[GetTerrainIndexRepeat(x + 1, y)].R);
                    var v2 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x + 1, y + 1)].R);
                    var v3 = new Vector3(x * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x, y + 1)].R);
                    var v4 = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE,
                        _data.HeightMap[GetTerrainIndexRepeat(x, y)].R);

                    var n1 = MathUtils.FaceNormalize(v1, v2, v3);
                    var n2 = MathUtils.FaceNormalize(v3, v4, v1);
                    _data.Normals[i] = n1 + n2;
                }
            }

            for (int i = 0; i < _data.Normals.Length; i++)
                _data.Normals[i] = Vector3.Normalize(_data.Normals[i]);
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
            float updateInterval = GetLightUpdateIntervalSeconds();
            if (updateInterval > 0f)
            {
                if (_lightUpdateTimer < updateInterval)
                    return;
                _lightUpdateTimer %= updateInterval;
            }
            else
            {
                _lightUpdateTimer = 0f;
            }

            _activeLightsVersion++;

            _activeLights.Clear();
            var world = _parent.World;
            if (_dynamicLights.Count == 0 || world == null)
            {
                ClearLightGridState();
                InvalidateLightCache();
                return;
            }

            bool isLoginScene = _parent.Scene is LoginScene;
            bool enableLowQualityCheck = Constants.ENABLE_LOW_QUALITY_SWITCH &&
                                         !(isLoginScene && !Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE) &&
                                         Camera.Instance != null;

            Vector2 cam2 = Vector2.Zero;
            float lowQualityDistSq = 0f;
            if (enableLowQualityCheck)
            {
                var camPos = Camera.Instance.Position;
                cam2 = new Vector2(camPos.X, camPos.Y);
                float d = Constants.LOW_QUALITY_DISTANCE;
                lowQualityDistSq = d * d;
            }

            for (int i = 0; i < _dynamicLights.Count; i++)
            {
                var light = _dynamicLights[i];
                if (light.Intensity <= 0.001f) continue;

                if (enableLowQualityCheck)
                {
                    var lightPos = new Vector2(light.Position.X, light.Position.Y);
                    if (Vector2.DistanceSquared(cam2, lightPos) > lowQualityDistSq)
                        continue;
                }

                if (!world.IsLightInView(light))
                    continue;

                // Snapshot values so lighting updates are throttled (not per-frame).
                _activeLights.Add(new DynamicLightSnapshot(light.Position, light.Color, light.Radius, light.Intensity));
            }

            // Always rebuild spatial grid when lights are updated (lights can move/change)
            RebuildLightGrid();

            // Invalidate cache on light updates to ensure fresh calculations
            InvalidateLightCache();
        }

        public Vector3 EvaluateDynamicLight(Vector2 position)
        {
            if (_precomputedActiveLights.Count == 0)
                return Vector3.Zero;

            // Cache is valid until active lights are updated again (throttled update cadence).
            if (_lightInfluenceCacheVersion != _activeLightsVersion)
            {
                _lightInfluenceCache.Clear();
                _lightInfluenceCacheVersion = _activeLightsVersion;
            }

            long posKey = GetPositionKey(position);
            if (_lightInfluenceCache.TryGetValue(posKey, out Vector3 cached))
                return cached;

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

                    if (_lightGridVersion[gx, gy] != _currentLightGridVersion)
                        continue;

                    var cellLights = _lightGrid[gx, gy];
                    foreach (int lightIndex in cellLights)
                    {
                        if ((uint)lightIndex >= (uint)_precomputedActiveLights.Count) // Safety guard
                            continue;

                        var light = _precomputedActiveLights[lightIndex];
                        var diff = light.Position - position;
                        float distSq = diff.LengthSquared();
                        if (distSq > light.RadiusSq) continue;

                        // Performance optimization: avoid expensive sqrt by using quadratic falloff
                        float normalizedDistSq = distSq * light.InvRadiusSq;
                        float factor = 1f - normalizedDistSq; // Quadratic falloff, no sqrt needed
                        result += light.ColorScaled * factor;
                    }
                }
            }

            if (_lightInfluenceCache.Count < MaxLightCacheEntries)
                _lightInfluenceCache[posKey] = result;

            return result;
        }

        private void RebuildLightGrid()
        {
            IncrementGridVersion();
            _precomputedActiveLights.Clear();

            // Add active lights to appropriate grid cells (use _activeLights which are already filtered)
            foreach (var light in _activeLights)
            {
                if (light.Intensity > 0.001f) // Double-check intensity
                {
                    int lightIndex = _precomputedActiveLights.Count;
                    var precomputedLight = PrecomputeLight(light);
                    _precomputedActiveLights.Add(precomputedLight);
                    AddLightToGrid(precomputedLight, lightIndex);
                }
            }
        }

        private void AddLightToGrid(PrecomputedLight light, int lightIndex)
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
                    if (_lightGridVersion[x, y] != _currentLightGridVersion)
                    {
                        _lightGrid[x, y].Clear();
                        _lightGridVersion[x, y] = _currentLightGridVersion;
                    }
                    _lightGrid[x, y].Add(lightIndex);
                }
            }
        }

        private static PrecomputedLight PrecomputeLight(DynamicLightSnapshot light)
        {
            float radius = light.Radius;
            float radiusSq = radius * radius;
            float invRadiusSq = radiusSq > 0.0001f ? 1f / radiusSq : 0f;

            return new PrecomputedLight
            {
                Position = new Vector2(light.Position.X, light.Position.Y),
                ColorScaled = light.Color * (255f * light.Intensity),
                Radius = radius,
                RadiusSq = radiusSq,
                InvRadiusSq = invRadiusSq
            };
        }

        private void ClearLightGridState()
        {
            IncrementGridVersion();
            _precomputedActiveLights.Clear();
        }

        private void IncrementGridVersion()
        {
            _currentLightGridVersion++;
            if (_currentLightGridVersion == int.MaxValue)
            {
                Array.Clear(_lightGridVersion, 0, _lightGridVersion.Length);
                _currentLightGridVersion = 1;
            }
        }

        private static long GetPositionKey(Vector2 position)
        {
            int x = (int)(position.X * InvCacheQuantization);
            int y = (int)(position.Y * InvCacheQuantization);
            return ((long)x << 32) | (uint)y;
        }

        private void InvalidateLightCache()
        {
            _lightInfluenceCache.Clear();
            _lightInfluenceCacheVersion = _activeLightsVersion;
        }

        private static float GetLightUpdateIntervalSeconds()
        {
            int fps = Constants.DYNAMIC_LIGHT_UPDATE_FPS;
            if (fps <= 0)
                return 0f;
            return 1f / fps;
        }

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;

        private static int GetTerrainIndexRepeat(int x, int y)
            => ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE)
             + (x & Constants.TERRAIN_SIZE_MASK);
    }
}
