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

        private readonly struct MeshCacheKey : IEquatable<MeshCacheKey>
        {
            public MeshCacheKey(int assetId, int meshIndex)
            {
                AssetId = assetId;
                MeshIndex = meshIndex;
            }

            public int AssetId { get; }
            public int MeshIndex { get; }

            public bool Equals(MeshCacheKey other) => AssetId == other.AssetId && MeshIndex == other.MeshIndex;

            public override bool Equals(object obj) => obj is MeshCacheKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(AssetId, MeshIndex);
        }

        // Enhanced cache state for GetModelBuffers to avoid redundant calculations
        private readonly Dictionary<MeshCacheKey, BufferCacheEntry> _bufferCacheState = [];
        // Per-mesh optimization: track which bones influence a mesh
        private readonly Dictionary<MeshCacheKey, short[]> _meshUsedBones = [];
        // Cache per (asset,mesh) vertex count to avoid per-frame summing
        private readonly Dictionary<MeshCacheKey, int> _meshVertexCountCache = [];
        // Track if index data has been uploaded for this (asset,mesh) so we can skip re-upload
        private readonly HashSet<MeshCacheKey> _indexInitialized = [];

        // Frame tracking for DISCARD/NoOverwrite optimization
        private uint _currentFrame = 0;
        private readonly Dictionary<MeshCacheKey, uint> _lastWriteFrame = [];
        // Track chosen index element size per mesh (true => 16-bit)
        private readonly Dictionary<MeshCacheKey, bool> _indexIs16Bit = [];

        // Per-frame instrumentation (queried by DebugPanel)
        public int FrameVBUpdates { get; private set; }
        public int FrameIBUploads { get; private set; }
        public int FrameVerticesTransformed { get; private set; }
        public int FrameMeshesProcessed { get; private set; }
        public int FrameCacheHits { get; private set; }
        public int FrameCacheMisses { get; private set; }

        // Snapshot of previous frame (stable for UI)
        public int LastFrameVBUpdates { get; private set; }
        public int LastFrameIBUploads { get; private set; }
        public int LastFrameVerticesTransformed { get; private set; }
        public int LastFrameMeshesProcessed { get; private set; }
        public int LastFrameCacheHits { get; private set; }
        public int LastFrameCacheMisses { get; private set; }
        
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
            _blendingConfig = new(StringComparer.OrdinalIgnoreCase);

            try
            {
                var asm = Assembly.GetExecutingAssembly();

                // Looking for exactly one resource ending with the file name
                var resName = asm.GetManifestResourceNames()
                                 .SingleOrDefault(n =>
                                     n.EndsWith("bmd_blending_config.json",
                                                StringComparison.OrdinalIgnoreCase));

                if (resName == null)
                {
                    _logger?.LogWarning(
                        "Embedded resource 'bmd_blending_config.json' not found " +
                        "(check Build Action = Embedded Resource and RootNamespace).");
                    return;
                }

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    _logger?.LogWarning($"Failed to open stream '{resName}'.");
                    return;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                using var doc = JsonDocument.Parse(json);
                var cleanObj = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

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
            // Snapshot previous frame for UI stability
            LastFrameVBUpdates = FrameVBUpdates;
            LastFrameIBUploads = FrameIBUploads;
            LastFrameVerticesTransformed = FrameVerticesTransformed;
            LastFrameMeshesProcessed = FrameMeshesProcessed;
            LastFrameCacheHits = FrameCacheHits;
            LastFrameCacheMisses = FrameCacheMisses;

            // Reset counters for the new frame
            _currentFrame++;
            FrameVBUpdates = 0;
            FrameIBUploads = 0;
            FrameVerticesTransformed = 0;
            FrameMeshesProcessed = 0;
            FrameCacheHits = 0;
            FrameCacheMisses = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 FastTransformPosition(in Matrix m, in System.Numerics.Vector3 p)
        {
            // Row-major transform (matching XNA):
            // x' = p.x*m.M11 + p.y*m.M21 + p.z*m.M31 + m.M41, etc.
            return new Vector3(
                p.X * m.M11 + p.Y * m.M21 + p.Z * m.M31 + m.M41,
                p.X * m.M12 + p.Y * m.M22 + p.Z * m.M32 + m.M42,
                p.X * m.M13 + p.Y * m.M23 + p.Z * m.M33 + m.M43);
        }

        public Task<BMD> Prepare(string path, string textureFolder = null)
        {
            lock (_bmds)
            {
                // Use original path as cache key for embedded resources
                string cacheKey = path;

                // Check if we should load from embedded resources
                if (path.Equals("Player/Player.bmd", StringComparison.OrdinalIgnoreCase))
                {
                    if (_bmds.TryGetValue(cacheKey, out Task<BMD> embeddedModelTask))
                        return embeddedModelTask;

                    embeddedModelTask = LoadEmbeddedAssetAsync(path, textureFolder);
                    _bmds.Add(cacheKey, embeddedModelTask);
                    return embeddedModelTask;
                }

                // Original file system loading
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
                // 'path' is already resolved to an absolute path in Prepare(); don't re-combine here.

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

                var dir = !string.IsNullOrEmpty(textureFolder)
                    ? textureFolder
                    : Path.GetRelativePath(Constants.DataPath, Path.GetDirectoryName(path));

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
            if (asset == null || boneMatrix == null || _graphicsDevice == null)
            {
                vertexBuffer = null;
                indexBuffer = null;
                return;
            }
            if (meshIndex < 0 || asset.Meshes == null || meshIndex >= asset.Meshes.Length)
            {
                vertexBuffer = null;
                indexBuffer = null;
                return;
            }

            var mesh = asset.Meshes[meshIndex];
            int assetId = RuntimeHelpers.GetHashCode(asset);
            var cacheKey = new MeshCacheKey(assetId, meshIndex);

            // Use cached vertex count where possible to avoid per-frame summing
            if (!_meshVertexCountCache.TryGetValue(cacheKey, out int totalVertices))
            {
                int vcount = 0;
                var tris = mesh.Triangles;
                for (int i = 0; i < tris.Length; i++) vcount += tris[i].Polygon;
                totalVertices = vcount;
                _meshVertexCountCache[cacheKey] = vcount;
            }
            int totalIndices = totalVertices;
            bool prefer16Bit = totalIndices <= ushort.MaxValue;

            // Create cache key based on asset and mesh
            // (reusing cacheKey defined above)
            
            // Calculate bone matrix hash for cache validation
            // Build or get the set of bones used by this mesh (distinct node indices)
            if (!_meshUsedBones.TryGetValue(cacheKey, out short[] usedBones))
            {
                var verts = mesh.Vertices;
                // Use HashSet to gather distinct nodes, then convert to array
                var set = new HashSet<short>();
                for (int i = 0; i < verts.Length; i++)
                {
                    short node = verts[i].Node;
                    if (node >= 0) set.Add(node);
                }
                usedBones = set.Count > 0 ? set.ToArray() : Array.Empty<short>();
                _meshUsedBones[cacheKey] = usedBones;
            }

            // Calculate a hash over only the bones influencing this mesh
            int boneMatrixHash = CalculateBoneMatrixHashSubset(boneMatrix, usedBones);
            
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
                FrameCacheHits++;
                return;
            }
            FrameCacheMisses++;
            FrameMeshesProcessed++;

            // Ensure buffers are properly sized
            if (vertexBuffer == null || vertexBuffer.VertexCount < totalVertices)
            {
                vertexBuffer?.Dispose();
                vertexBuffer = new DynamicVertexBuffer(
                    _graphicsDevice,
                    VertexPositionColorNormalTexture.VertexDeclaration,
                    totalVertices,
                    BufferUsage.WriteOnly);
            }

            bool createdOrResizedIndex = false;
            bool mismatchIndexSize = false;
            if (_indexIs16Bit.TryGetValue(cacheKey, out bool prevIs16) && prevIs16 != prefer16Bit)
                mismatchIndexSize = true;

            if (indexBuffer == null || indexBuffer.IndexCount < totalIndices || mismatchIndexSize)
            {
                indexBuffer?.Dispose();
                indexBuffer = new DynamicIndexBuffer(
                    _graphicsDevice,
                    prefer16Bit ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits,
                    totalIndices,
                    BufferUsage.WriteOnly);
                createdOrResizedIndex = true;
                _indexIs16Bit[cacheKey] = prefer16Bit;
            }

            // Build vertex data with unique-vertex transform caching
            VertexPositionColorNormalTexture[] vertices = null;
            Vector3[] posCache = null;
            bool[] visited = null;

            try
            {
                vertices = ArrayPool<VertexPositionColorNormalTexture>.Shared.Rent(totalVertices);
                posCache = ArrayPool<Vector3>.Shared.Rent(mesh.Vertices.Length);
                visited = ArrayPool<bool>.Shared.Rent(mesh.Vertices.Length);
                Array.Clear(visited, 0, mesh.Vertices.Length);

                int v = 0;
                int uniqueTransformed = 0;
                foreach (var tri in mesh.Triangles)
                {
                    for (int j = 0; j < tri.Polygon; j++)
                    {
                        int vi = tri.VertexIndex[j];

                        if (!visited[vi])
                        {
                            visited[vi] = true;
                            uniqueTransformed++;
                            var vert = mesh.Vertices[vi];
                            if (vert.Node >= 0 && vert.Node < boneMatrix.Length)
                            {
                                posCache[vi] = FastTransformPosition(in boneMatrix[vert.Node], in vert.Position);
                            }
                            else
                            {
                                posCache[vi] = vert.Position;
                            }
                        }

                        int ni = tri.NormalIndex[j];
                        var normal = mesh.Normals[ni].Normal; // keep as-is (object space path)

                        int ti = tri.TexCoordIndex[j];
                        var uv = mesh.TexCoords[ti];

                        vertices[v] = new VertexPositionColorNormalTexture(
                            posCache[vi],
                            color,
                            normal,
                            new Vector2(uv.U, uv.V));
                        v++;
                    }
                }

                // Optimize SetData with DISCARD/NoOverwrite based on frame tracking
                bool isFirstWriteThisFrame = !_lastWriteFrame.TryGetValue(cacheKey, out uint lastFrame) || lastFrame != _currentFrame;
                var setDataOptions = isFirstWriteThisFrame ? SetDataOptions.Discard : SetDataOptions.NoOverwrite;

                vertexBuffer.SetData(vertices, 0, totalVertices, setDataOptions);
                FrameVBUpdates++;
                FrameVerticesTransformed += uniqueTransformed;

                // Upload index data only if needed (new or resized buffer or not yet initialized)
                if (createdOrResizedIndex || !_indexInitialized.Contains(cacheKey))
                {
                    if (prefer16Bit)
                    {
                        var indices16 = ArrayPool<ushort>.Shared.Rent(totalIndices);
                        try
                        {
                            for (int i = 0; i < totalIndices; i++) indices16[i] = (ushort)i;
                            indexBuffer.SetData(indices16, 0, totalIndices, SetDataOptions.Discard);
                        }
                        finally
                        {
                            ArrayPool<ushort>.Shared.Return(indices16, clearArray: true);
                        }
                    }
                    else
                    {
                        var indices32 = ArrayPool<int>.Shared.Rent(totalIndices);
                        try
                        {
                            for (int i = 0; i < totalIndices; i++) indices32[i] = i;
                            indexBuffer.SetData(indices32, 0, totalIndices, SetDataOptions.Discard);
                        }
                        finally
                        {
                            ArrayPool<int>.Shared.Return(indices32, clearArray: true);
                        }
                    }

                    _indexInitialized.Add(cacheKey);
                    FrameIBUploads++;
                }

                // Update frame tracking
                _lastWriteFrame[cacheKey] = _currentFrame;

                // Update cache entry only if caching is enabled
                if (!skipCache)
                {
                    _bufferCacheState[cacheKey] = new BufferCacheEntry(color, boneMatrixHash);
                }
            }
            finally
            {
                if (vertices != null)
                {
                    ArrayPool<VertexPositionColorNormalTexture>.Shared.Return(vertices);
                }

                if (posCache != null)
                {
                    ArrayPool<Vector3>.Shared.Return(posCache);
                }

                if (visited != null)
                {
                    ArrayPool<bool>.Shared.Return(visited, clearArray: true);
                }
            }
        }

        private async Task<BMD> LoadEmbeddedAssetAsync(string originalPath, string textureFolder = null)
        {
            try
            {
                _logger?.LogDebug($"Loading embedded resource: {originalPath}");

                // Map the path to the embedded resource name
                var resourceName = GetEmbeddedResourceName(originalPath);
                if (resourceName == null)
                {
                    _logger?.LogWarning($"Embedded resource not found for path: {originalPath}");
                    return null;
                }

                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger?.LogWarning($"Cannot open embedded resource stream: {resourceName}");
                    return null;
                }

                var buffer = new byte[stream.Length];
                await stream.ReadExactlyAsync(buffer, 0, (int)stream.Length);

                var asset = _reader.ReadFromBuffer(buffer);

                // for custom blending from json
                var relativePath = originalPath.Replace("\\", "/");
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

                var texturePathMap = new Dictionary<string, string>();

                lock (_texturePathMap)
                    _texturePathMap.Add(asset, texturePathMap);

                var dir = !string.IsNullOrEmpty(textureFolder)
                    ? textureFolder
                    : Path.GetDirectoryName(originalPath.Replace("/", Path.DirectorySeparatorChar.ToString()));

                var tasks = new List<Task>();
                foreach (var mesh in asset.Meshes)
                {
                    var fullPath = Path.Combine(dir, mesh.TexturePath);
                    if (texturePathMap.TryAdd(mesh.TexturePath.ToLowerInvariant(), fullPath))
                        tasks.Add(TextureLoader.Instance.Prepare(fullPath));
                }

                await Task.WhenAll(tasks);

                _logger?.LogDebug($"Successfully loaded embedded resource: {originalPath}");
                return asset;
            }
            catch (Exception e)
            {
                _logger?.LogError(e, $"Failed to load embedded asset {originalPath}: {e.Message}");
                return null;
            }
        }

        private string GetEmbeddedResourceName(string path)
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceNames = asm.GetManifestResourceNames();

            // Map specific paths to their embedded resource names
            return path switch
            {
                "Player/Player.bmd" => resourceNames.FirstOrDefault(n => n.EndsWith("player.bmd", StringComparison.OrdinalIgnoreCase)),
                _ => null
            };
        }

        private int CalculateBoneMatrixHashSubset(Matrix[] boneMatrix, short[] usedBones)
        {
            if (boneMatrix == null || usedBones == null || usedBones.Length == 0) return 0;
            int hash = 17;
            for (int i = 0; i < usedBones.Length; i++)
            {
                int idx = usedBones[i];
                if ((uint)idx >= (uint)boneMatrix.Length) continue;
                ref var m = ref boneMatrix[idx];
                hash = hash * 31 + m.Translation.GetHashCode();
                // Include more rotation/scale components to reduce false cache hits
                hash = hash * 31 + m.M11.GetHashCode();
                hash = hash * 31 + m.M12.GetHashCode();
                hash = hash * 31 + m.M13.GetHashCode();
                hash = hash * 31 + m.M21.GetHashCode();
                hash = hash * 31 + m.M22.GetHashCode();
                hash = hash * 31 + m.M23.GetHashCode();
                hash = hash * 31 + m.M31.GetHashCode();
                hash = hash * 31 + m.M32.GetHashCode();
                hash = hash * 31 + m.M33.GetHashCode();
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
            _lastWriteFrame.Clear();
            _indexInitialized.Clear();
            _indexIs16Bit.Clear();
        }
    }

}
