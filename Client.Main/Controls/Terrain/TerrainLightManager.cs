using Client.Main.Scenes;
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

        public IReadOnlyList<DynamicLight> DynamicLights => _dynamicLights;

        public TerrainLightManager(TerrainData data, GameControl parent)
        {
            _data = data;
            _parent = parent;
        }

        public void AddDynamicLight(DynamicLight light) => _dynamicLights.Add(light);
        public void RemoveDynamicLight(DynamicLight light) => _dynamicLights.Remove(light);

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

        public void UpdateActiveLights()
        {
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
        }

        public Vector3 EvaluateDynamicLight(Vector2 position)
        {
            Vector3 result = Vector3.Zero;
            foreach (var light in _activeLights)
            {
                if (light.Intensity <= 0.001f) continue;

                var diff = new Vector2(light.Position.X, light.Position.Y) - position;
                float distSq = diff.LengthSquared();
                float radiusSq = light.Radius * light.Radius;
                if (distSq > radiusSq) continue;

                float dist = MathF.Sqrt(distSq);
                float factor = 1f - dist / light.Radius;
                result += light.Color * (255f * light.Intensity * factor);
            }
            return result;
        }

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;

        private static int GetTerrainIndexRepeat(int x, int y)
            => ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE)
             + (x & Constants.TERRAIN_SIZE_MASK);
    }
}
