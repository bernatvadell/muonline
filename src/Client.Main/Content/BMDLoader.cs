using Client.Data;
using Client.Data.BMD;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Content
{
    public class BMDLoader
    {
        public static BMDLoader Instance { get; } = new BMDLoader();

        private readonly BMDReader _reader = new BMDReader();
        private Dictionary<string, Task<BMD>> _bmds = [];

        private Dictionary<BMD, Dictionary<string, string>> _texturePathMap = new Dictionary<BMD, Dictionary<string, string>>();
        private Dictionary<BMD, VertexBuffer[]> _vertexBuffers = new Dictionary<BMD, VertexBuffer[]>();
        private Dictionary<BMD, IndexBuffer[]> _indexBuffers = new Dictionary<BMD, IndexBuffer[]>();
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

                foreach (var mesh in asset.Meshes)
                {
                    var fullPath = Path.Combine(dir, mesh.TexturePath);
                    texturePathMap.Add(mesh.TexturePath.ToLowerInvariant(), fullPath);
                    await TextureLoader.Instance.Prepare(fullPath);
                }

                // Inicializar los vértices y los índices solo una vez para este BMD
                InitializeBuffers(asset);

                return asset;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load asset {path}: {e.Message}");
                return null;
            }
        }


        private void InitializeBuffers(BMD asset)
        {
            if (_vertexBuffers.ContainsKey(asset))
                return; // Los buffers ya están creados, no necesitamos volver a hacerlo.

            var allVertices = new VertexBuffer[asset.Meshes.Length];
            var allIndices = new IndexBuffer[asset.Meshes.Length];

            for (var m = 0; m < asset.Meshes.Length; m++)
            {
                var mesh = asset.Meshes[m];
                var vertices = new List<VertexPositionColorNormalTexture>();
                var indices = new List<int>();

                foreach (var triangle in mesh.Triangles)
                {
                    for (int i = 0; i < triangle.Polygon; i++)
                    {
                        try
                        {
                            var vertexIndex = triangle.VertexIndex[i];
                            var vertex = mesh.Vertices[vertexIndex];

                            // Obtener la normal asociada al vértice
                            var normalIndex = triangle.NormalIndex[i];
                            var normal = mesh.Normals[normalIndex].Normal; // Extrae la normal desde la estructura

                            var coordIndex = triangle.TexCoordIndex[i];
                            var texCoord = mesh.TexCoords[coordIndex];

                            // Agregar el vértice con posición, color, normal y coordenadas de textura
                            vertices.Add(new VertexPositionColorNormalTexture(
                                vertex.Position,             // Posición del vértice
                                Color.White,                 // Color (puedes cambiar esto según sea necesario)
                                normal,                      // Normal del vértice
                                new Vector2(texCoord.U, texCoord.V) // Coordenadas de textura
                            ));

                            indices.Add(vertices.Count - 1); // Agregar el índice en orden de vértices
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Error creating vertex", e);
                        }
                    }
                }

                // Crear los buffers y almacenar en caché
                VertexBuffer vertexBuffer = new VertexBuffer(
                    _graphicsDevice,
                    VertexPositionColorNormalTexture.VertexDeclaration,
                    vertices.Count,
                    BufferUsage.None);

                vertexBuffer.SetData(vertices.ToArray());

                IndexBuffer indexBuffer = new IndexBuffer(
                    _graphicsDevice,
                    IndexElementSize.ThirtyTwoBits,
                    indices.Count,
                    BufferUsage.None);

                indexBuffer.SetData(indices.ToArray());

                allVertices[m] = vertexBuffer;
                allIndices[m] = indexBuffer;
            }

            lock (_vertexBuffers)
                _vertexBuffers[asset] = allVertices;

            lock (_indexBuffers)
                _indexBuffers[asset] = allIndices;
        }


        public VertexBuffer GetVertexBuffer(BMD asset, int meshIndex)
        {
            if (_vertexBuffers.TryGetValue(asset, out var vertexBuffers) && vertexBuffers.Length > meshIndex)
                return vertexBuffers[meshIndex];

            return null;
        }

        public IndexBuffer GetIndexBuffer(BMD asset, int meshIndex)
        {
            if (_indexBuffers.TryGetValue(asset, out var indexBuffers) && indexBuffers.Length > meshIndex)
                return indexBuffers[meshIndex];

            return null;
        }

        public string GetTexturePath(BMD bmd, string texturePath)
        {
            texturePath = texturePath.ToLowerInvariant();

            string result = null;

            if (_texturePathMap.TryGetValue(bmd, out Dictionary<string, string> value) && value.ContainsKey(texturePath))
                result = value[texturePath];

            if (result == null)
                Debug.WriteLine($"Texture path not found: {texturePath}");

            return result;
        }
    }

}
