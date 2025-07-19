using Client.Data.ATT;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Client.Main.Utils;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Handles loading of all terrain-related assets from files.
    /// </summary>
    public class TerrainLoader
    {
        private readonly short _worldIndex;
        private readonly TerrainData _terrainData;

        public TerrainLoader(short worldIndex)
        {
            _worldIndex = worldIndex;
            _terrainData = new TerrainData();
        }

        public void SetTextureMapping(Dictionary<int, string> textureMapping)
        {
            _terrainData.TextureMappingFiles = new Dictionary<int, string>(textureMapping);
        }

        public async Task<TerrainData> LoadAsync()
        {
            var terrainReader = new ATTReader();
            var ozbReader = new OZBReader();
            var mappingReader = new MapReader();

            var tasks = new List<Task>();
            var worldFolder = $"World{_worldIndex}";
            var fullPathWorldFolder = Path.Combine(Constants.DataPath, worldFolder);

            if (string.IsNullOrEmpty(fullPathWorldFolder) || !Directory.Exists(fullPathWorldFolder))
                return null;

            // Load terrain .att
            string attPath = GetActualPath(Path.Combine(fullPathWorldFolder, $"EncTerrain{_worldIndex}.att"));
            if (!string.IsNullOrEmpty(attPath))
                tasks.Add(terrainReader.Load(attPath).ContinueWith(t => _terrainData.Attributes = t.Result));

            // Load base terrain height map
            string heightPath = GetActualPath(Path.Combine(fullPathWorldFolder, "TerrainHeight.OZB"));
            if (!string.IsNullOrEmpty(heightPath))
                tasks.Add(ozbReader.Load(heightPath)
                    .ContinueWith(t => _terrainData.HeightMap = t.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));

            // Load terrain mapping (.map)
            string mapPath = GetActualPath(Path.Combine(fullPathWorldFolder, $"EncTerrain{_worldIndex}.map"));
            if (!string.IsNullOrEmpty(mapPath))
                tasks.Add(mappingReader.Load(mapPath).ContinueWith(t => _terrainData.Mapping = t.Result));

            // Prepare texture file list
            var textureMapFiles = new string[256];
            foreach (var kvp in _terrainData.TextureMappingFiles)
            {
                textureMapFiles[kvp.Key] = GetActualPath(Path.Combine(fullPathWorldFolder, kvp.Value));
            }
            for (int i = 1; i <= 16; i++)
            {
                var extTilePath = GetActualPath(Path.Combine(fullPathWorldFolder, $"ExtTile{i:00}.ozj"));
                textureMapFiles[13 + i] = extTilePath;
            }

            _terrainData.Textures = new Microsoft.Xna.Framework.Graphics.Texture2D[textureMapFiles.Length];
            for (int t = 0; t < textureMapFiles.Length; t++)
            {
                var path = textureMapFiles[t];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                int textureIndex = t;
                tasks.Add(TextureLoader.Instance.Prepare(path)
                    .ContinueWith(_ => _terrainData.Textures[textureIndex] = TextureLoader.Instance.GetTexture2D(path)));
            }

            // Load lightmap or default to white
            string textureLightPath = GetActualPath(Path.Combine(fullPathWorldFolder, "TerrainLight.OZB"));
            if (!string.IsNullOrEmpty(textureLightPath) && File.Exists(textureLightPath))
            {
                tasks.Add(ozbReader.Load(textureLightPath)
                    .ContinueWith(ozb => _terrainData.LightData = ozb.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            }
            else
            {
                _terrainData.LightData = Enumerable.Repeat(Microsoft.Xna.Framework.Color.White, Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE).ToArray();
            }

            await Task.WhenAll(tasks);

            _terrainData.GrassWind = new float[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            return _terrainData;
        }
    }
}
