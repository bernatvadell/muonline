using Client.Data;
using Client.Data.BMD;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        public Task<BMD> Prepare(string path)
        {
            lock (_bmds)
            {
                if (_bmds.TryGetValue(path, out Task<BMD> modelTask))
                    return modelTask;

                modelTask = LoadAssetAsync(path);
                _bmds.Add(path, modelTask);
                return modelTask;
            }
        }

        private async Task<BMD> LoadAssetAsync(string path)
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

                var dir = Path.GetRelativePath(Constants.DataPath, Path.GetDirectoryName(path));

                var tasks = new List<Task>();
                foreach (var mesh in asset.Meshes)
                {
                    var fullPath = Path.Combine(dir, mesh.TexturePath);
                    texturePathMap.Add(mesh.TexturePath.ToLowerInvariant(), fullPath);
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

        public void GetModelBuffers(BMD asset, int meshIndex, Color color, Matrix[] boneMatrix, out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer)
        {
            var mesh = asset.Meshes[meshIndex];

            int totalVertices = mesh.Triangles.Sum(triangle => triangle.Polygon);
            int totalIndices = totalVertices;

            var vertices = new VertexPositionColorNormalTexture[totalVertices];
            var indices = new int[totalIndices];

            var pi = 0;

            for (var i = 0; i < mesh.Triangles.Length; i++)
            {
                var triangle = mesh.Triangles[i];

                for (int j = 0; j < triangle.Polygon; j++)
                {
                    var vertexIndex = triangle.VertexIndex[j];
                    var vertex = mesh.Vertices[vertexIndex];

                    var normalIndex = triangle.NormalIndex[j];
                    var normal = mesh.Normals[normalIndex].Normal;

                    var coordIndex = triangle.TexCoordIndex[j];
                    var texCoord = mesh.TexCoords[coordIndex];

                    var pos = Vector3.Transform(vertex.Position, boneMatrix[vertex.Node]);

                    vertices[pi] = new VertexPositionColorNormalTexture(
                        pos,
                        color,
                        normal,
                        new Vector2(texCoord.U, texCoord.V)
                    );

                    indices[pi] = pi;

                    pi++;
                }
            }
            
            _graphicsDevice.SetRenderTarget(null);

            vertexBuffer = new DynamicVertexBuffer(_graphicsDevice, VertexPositionColorNormalTexture.VertexDeclaration, totalVertices, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            indexBuffer = new DynamicIndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, totalIndices, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
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
