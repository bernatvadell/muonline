using Client.Data.BMD;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Client.Main.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Client.Main.Content
{
    public class BMDLoader
    {
        public static BMDLoader Instance { get; } = new BMDLoader();

        private readonly BMDReader _reader = new();
        private readonly Dictionary<string, Task<BMD>> _bmds = [];
        private readonly Dictionary<BMD, Dictionary<string, string>> _texturePathMap = [];
        private Dictionary<string, Dictionary<int, string>> _blendingConfig;
        
        // Enhanced cache for GetModelBuffers to avoid redundant calculations
        private readonly Dictionary<string, (DynamicVertexBuffer vertexBuffer, DynamicIndexBuffer indexBuffer)> _bufferCache = [];
        private readonly Dictionary<string, BufferCacheEntry> _bufferCacheState = [];
        
        // Frame tracking for DISCARD/NoOverwrite optimization
        private uint _currentFrame = 0;
        private readonly Dictionary<string, uint> _lastWriteFrame = [];
        
        private struct BufferCacheEntry
        {
            public Color LastColor;
            public int LastBoneMatrixHash;
            public bool IsValid;
            
            public BufferCacheEntry(Color color, int boneMatrixHash)
            {
                LastColor = color;
                LastBoneMatrixHash = boneMatrixHash;
                IsValid = true;
            }
        }

        private GraphicsDevice _graphicsDevice;
        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<BMDLoader>();

        // for custom blending from json

        private BMDLoader()
        {
            LoadBlendingConfig();
        }

        private void LoadBlendingConfig()
        {
            _blendingConfig = new();

            try
            {
                var asm = Assembly.GetExecutingAssembly();

                // Szukamy dokładnie jednego zasobu kończącego się nazwą pliku
                var resName = asm.GetManifestResourceNames()
                                 .SingleOrDefault(n =>
                                     n.EndsWith("bmd_blending_config.json",
                                                StringComparison.OrdinalIgnoreCase));

                if (resName == null)
                {
                    _logger?.LogWarning(
                        "Embedded resource 'bmd_blending_config.json' not found " +
                        "(sprawdź Build Action = Embedded Resource i RootNamespace).");
                    return;
                }

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    _logger?.LogWarning($"Nie udało się otworzyć strumienia '{resName}'.");
                    return;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                using var doc = JsonDocument.Parse(json);
                var cleanObj = new Dictionary<string, Dictionary<int, string>>();

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.StartsWith("comment", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var innerDict = new Dictionary<int, string>();
                    foreach (var mesh in prop.Value.EnumerateObject())
                        innerDict[int.Parse(mesh.Name)] = mesh.Value.GetString();

                    cleanObj[prop.Name] = innerDict;
                }

                _blendingConfig = cleanObj;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load embedded BMD blending config.");
            }
        }

        //

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }
        
        /// <summary>
        /// Call this at the start of each frame to enable DISCARD/NoOverwrite optimization
        /// </summary>
        public void BeginFrame()
        {
            _currentFrame++;
        }

        public Task<BMD> Prepare(string path, string textureFolder = null)
        {
            lock (_bmds)
            {
                path = GetActualPath(Path.Combine(Constants.DataPath, path));
                if (_bmds.TryGetValue(path, out Task<BMD> modelTask))
                    return modelTask;

                modelTask = LoadAssetAsync(path, textureFolder);
                _bmds.Add(path, modelTask);
                return modelTask;
            }
        }

        private async Task<BMD> LoadAssetAsync(string path, string textureFolder = null)
        {
            try
            {
                path = Path.Combine(Constants.DataPath, path);

                if (!File.Exists(path))
                {
                    _logger?.LogDebug($"Model not found: {path}");
                    return null;
                }

                var asset = await _reader.Load(path);

                // for custom blending from json
                var relativePath = Path.GetRelativePath(Constants.DataPath, path).Replace("\\", "/");
                if (_blendingConfig.TryGetValue(relativePath, out var meshConfig))
                {
                    for (int i = 0; i < asset.Meshes.Length; i++)
                    {
                        if (meshConfig.TryGetValue(i, out var blendMode))
                        {
                            asset.Meshes[i].BlendingMode = blendMode;
                        }
                    }
                }
                //

                var texturePathMap = new Dictionary<string, string>();

                lock (_texturePathMap)
                    _texturePathMap.Add(asset, texturePathMap);

                var dir = string.IsNullOrEmpty(textureFolder)
                    ? Path.GetRelativePath(Constants.DataPath, Path.GetDirectoryName(path))
                    : null;

                var tasks = new List<Task>();
                foreach (var mesh in asset.Meshes)
                {
                    var fullPath = Path.Combine(dir, mesh.TexturePath);
                    if (texturePathMap.TryAdd(mesh.TexturePath.ToLowerInvariant(), fullPath))
                        tasks.Add(TextureLoader.Instance.Prepare(fullPath));
                }

                await Task.WhenAll(tasks);

                return asset;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load asset {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds (or updates) the dynamic vertex/index buffers for the given mesh.
        /// Uses ArrayPool to eliminate per‑frame allocations and intelligent caching.
        /// </summary>
        public void GetModelBuffers(
         BMD asset,
         int meshIndex,
         Color color,
         Matrix[] boneMatrix,
         ref DynamicVertexBuffer vertexBuffer,
         ref DynamicIndexBuffer indexBuffer,
         bool skipCache = false)
        {
            if (boneMatrix == null)
            {
                vertexBuffer = null;
                indexBuffer = null;
                return;
            }

            var mesh = asset.Meshes[meshIndex];
            int totalVertices = mesh.Triangles.Sum(t => t.Polygon);
            int totalIndices = totalVertices;

            // Create cache key based on asset and mesh
            string cacheKey = $"{asset.GetHashCode()}_{meshIndex}";
            
            // Calculate bone matrix hash for cache validation
            int boneMatrixHash = CalculateBoneMatrixHash(boneMatrix);
            
            // Check if we can use cached data (only if caching is enabled)
            if (!skipCache && 
                _bufferCacheState.TryGetValue(cacheKey, out var cacheEntry) &&
                cacheEntry.IsValid &&
                cacheEntry.LastColor == color &&
                cacheEntry.LastBoneMatrixHash == boneMatrixHash &&
                vertexBuffer != null &&
                indexBuffer != null)
            {
                // Cache hit - reuse existing buffers
                return;
            }

            // Ensure buffers are properly sized
            if (vertexBuffer == null || vertexBuffer.VertexCount < totalVertices)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = new DynamicVertexBuffer(
                    _graphicsDevice,
                    VertexPositionColorNormalTexture.VertexDeclaration,
                    totalVertices,
                    BufferUsage.None);
            }

            if (indexBuffer == null || indexBuffer.IndexCount < totalIndices)
            {
                indexBuffer?.Dispose();
                indexBuffer = new DynamicIndexBuffer(
                    _graphicsDevice,
                    IndexElementSize.ThirtyTwoBits,
                    totalIndices,
                    BufferUsage.None);
            }

            // Build vertex and index data
            var vertices = ArrayPool<VertexPositionColorNormalTexture>.Shared.Rent(totalVertices);
            var indices = ArrayPool<int>.Shared.Rent(totalIndices);

            int v = 0;
            foreach (var tri in mesh.Triangles)
            {
                for (int j = 0; j < tri.Polygon; j++)
                {
                    int vi = tri.VertexIndex[j];
                    var vert = mesh.Vertices[vi];

                    int ni = tri.NormalIndex[j];
                    var normal = mesh.Normals[ni].Normal;

                    int ti = tri.TexCoordIndex[j];
                    var uv = mesh.TexCoords[ti];

                    Vector3 pos = Vector3.Transform(vert.Position, 
                        vert.Node < boneMatrix.Length ? boneMatrix[vert.Node] : Matrix.Identity);

                    vertices[v] = new VertexPositionColorNormalTexture(
                        pos,
                        color,
                        normal,
                        new Vector2(uv.U, uv.V));

                    indices[v] = v;
                    v++;
                }
            }

            // Optimize SetData with DISCARD/NoOverwrite based on frame tracking
            bool isFirstWriteThisFrame = !_lastWriteFrame.TryGetValue(cacheKey, out uint lastFrame) || lastFrame != _currentFrame;
            var setDataOptions = isFirstWriteThisFrame ? SetDataOptions.Discard : SetDataOptions.NoOverwrite;
            
            vertexBuffer.SetData(vertices, 0, totalVertices, setDataOptions);
            indexBuffer.SetData(indices, 0, totalIndices, setDataOptions);
            
            // Update frame tracking
            _lastWriteFrame[cacheKey] = _currentFrame;

            ArrayPool<VertexPositionColorNormalTexture>.Shared.Return(vertices);
            ArrayPool<int>.Shared.Return(indices, clearArray: true);
            
            // Update cache entry only if caching is enabled
            if (!skipCache)
            {
                _bufferCacheState[cacheKey] = new BufferCacheEntry(color, boneMatrixHash);
            }
        }

        private int CalculateBoneMatrixHash(Matrix[] boneMatrix)
        {
            if (boneMatrix == null) return 0;

            int hash = 17;

            for (int i = 0; i < boneMatrix.Length; i++)
            {
                // Include translation, rotation components, and scale for proper cache validation
                ref var matrix = ref boneMatrix[i];
                hash = hash * 31 + matrix.Translation.GetHashCode();
                hash = hash * 31 + matrix.M11.GetHashCode(); // Scale/rotation X
                hash = hash * 31 + matrix.M22.GetHashCode(); // Scale/rotation Y  
                hash = hash * 31 + matrix.M33.GetHashCode(); // Scale/rotation Z
                hash = hash * 31 + matrix.M12.GetHashCode(); // Rotation component
                hash = hash * 31 + matrix.M21.GetHashCode(); // Rotation component
            }
            return hash;
        }

        public string GetTexturePath(BMD bmd, string texturePath)
        {
            texturePath = texturePath.ToLowerInvariant();

            string result = null;

            if (_texturePathMap.TryGetValue(bmd, out Dictionary<string, string> value) && value.TryGetValue(texturePath, out string fullTexturePath))
                result = fullTexturePath;

            if (result == null)
                _logger?.LogDebug($"Texture path not found: {texturePath}");

            return result;
        }
        
        // Clear cache when needed (e.g., when objects are disposed)
        public void ClearBufferCache()
        {
            _bufferCacheState.Clear();
        }
    }

}