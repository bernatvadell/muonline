using Client.Data.BMD;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Client.Main.Utils;

namespace Client.Main.Content
{
    public class BMDLoader
    {
        public static BMDLoader Instance { get; } = new BMDLoader();

        private readonly BMDReader _reader = new();
        private readonly Dictionary<string, Task<BMD>> _bmds = [];
        private readonly Dictionary<BMD, Dictionary<string, string>> _texturePathMap = [];

        private GraphicsDevice _graphicsDevice;

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
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
                    Debug.WriteLine($"Model not found: {path}");
                    return null;
                }

                var asset = await _reader.Load(path);
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
        /// Uses ArrayPool to eliminate per‑frame allocations.
        /// </summary>
        public void GetModelBuffers(
         BMD asset,
         int meshIndex,
         Color color,
         Matrix[] boneMatrix,
         ref DynamicVertexBuffer vertexBuffer,
         ref DynamicIndexBuffer indexBuffer)
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

                    Vector3 pos = Vector3.Transform(vert.Position, boneMatrix[vert.Node]);

                    vertices[v] = new VertexPositionColorNormalTexture(
                        pos,
                        color,
                        normal,
                        new Vector2(uv.U, uv.V));

                    indices[v] = v;
                    v++;
                }
            }

            vertexBuffer.SetData(vertices, 0, totalVertices, SetDataOptions.Discard);
            indexBuffer.SetData(indices, 0, totalIndices, SetDataOptions.Discard);

            ArrayPool<VertexPositionColorNormalTexture>.Shared.Return(vertices);
            ArrayPool<int>.Shared.Return(indices, clearArray: true);
        }

        public string GetTexturePath(BMD bmd, string texturePath)
        {
            texturePath = texturePath.ToLowerInvariant();

            string result = null;

            if (_texturePathMap.TryGetValue(bmd, out Dictionary<string, string> value) && value.TryGetValue(texturePath, out string fullTexturePath))
                result = fullTexturePath;

            if (result == null)
                Debug.WriteLine($"Texture path not found: {texturePath}");

            return result;
        }
    }

}
