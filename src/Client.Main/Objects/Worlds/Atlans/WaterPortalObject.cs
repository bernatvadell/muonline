using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;
using Client.Data.BMD;

namespace Client.Main.Objects.Worlds.Atlans
{
    public class WaterPortalObject : ModelObject
    {
        private const float TEXTURE_SCROLL_SPEED = 0.2f;
        private const float WAVE_FREQUENCY = 1.5f;
        private const float WAVE_AMPLITUDE = 0.4f;
        private const float VERTEX_WAVE_HEIGHT = 0.4f;
        private const float SPATIAL_FREQUENCY = 0.8f;

        private float _currentOffset = 0f;
        private float _waveTime = 0f;
        private readonly float _fixedTimeStep = 1f / 30f;
        private float _accumulatedTime = 0f;

        private BMDTexCoord[] _originalTexCoords;
        private BMDTextureVertex[] _originalVertices;
        private System.Numerics.Vector3[] _vertexOffsets;

        public override async Task Load()
        {
            BlendState = BlendState.NonPremultiplied;
            LightEnabled = true;
            IsTransparent = true;
            BlendMeshState = BlendState.Additive;
            Alpha = 0.5f;
            Model = await BMDLoader.Instance.Prepare($"Object8/Object24.bmd");

            await base.Load();

            if (Model?.Meshes != null && Model.Meshes.Length > 0)
            {
                var mesh = Model.Meshes[0];
                _originalTexCoords = new BMDTexCoord[mesh.TexCoords.Length];
                Array.Copy(mesh.TexCoords, _originalTexCoords, mesh.TexCoords.Length);

                _originalVertices = new BMDTextureVertex[mesh.Vertices.Length];
                Array.Copy(mesh.Vertices, _originalVertices, mesh.Vertices.Length);

                _vertexOffsets = new System.Numerics.Vector3[mesh.Vertices.Length];
            }
        }

        private float CalculateWaveOffset(float x, float z, float time)
        {
            float wave1 = (float)Math.Sin(time * WAVE_FREQUENCY + (x + z) * SPATIAL_FREQUENCY);
            float wave2 = (float)Math.Sin(time * WAVE_FREQUENCY * 0.5f + (x - z) * SPATIAL_FREQUENCY * 1.3f) * 0.5f;
            float wave3 = (float)Math.Cos(time * WAVE_FREQUENCY * 0.7f + z * SPATIAL_FREQUENCY * 0.8f) * 0.3f;

            return (wave1 + wave2 + wave3) * 0.33f;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Model?.Meshes == null || Model.Meshes.Length == 0 || _originalTexCoords == null)
                return;

            _accumulatedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            _waveTime += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.5f;

            while (_accumulatedTime >= _fixedTimeStep)
            {
                _currentOffset = (_currentOffset + TEXTURE_SCROLL_SPEED * _fixedTimeStep) % 1.0f;
                _accumulatedTime -= _fixedTimeStep;
            }

            var mesh = Model.Meshes[0];

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var originalVertex = _originalVertices[i];
                float waveOffset = CalculateWaveOffset(
                    originalVertex.Position.X,
                    originalVertex.Position.Z,
                    _waveTime
                );

                _vertexOffsets[i] = new System.Numerics.Vector3(0, waveOffset * VERTEX_WAVE_HEIGHT, 0);
            }

            foreach (var triangle in mesh.Triangles)
            {
                for (int i = 0; i < triangle.VertexIndex.Length; i++)
                {
                    if (triangle.VertexIndex[i] < 0) continue;

                    System.Numerics.Vector3 avgOffset = System.Numerics.Vector3.Zero;
                    int validNeighbors = 0;

                    for (int j = 0; j < triangle.VertexIndex.Length; j++)
                    {
                        if (triangle.VertexIndex[j] >= 0)
                        {
                            avgOffset += _vertexOffsets[triangle.VertexIndex[j]];
                            validNeighbors++;
                        }
                    }

                    if (validNeighbors > 0)
                    {
                        avgOffset /= validNeighbors;
                        int vertexIdx = triangle.VertexIndex[i];
                        _vertexOffsets[vertexIdx] = System.Numerics.Vector3.Lerp(_vertexOffsets[vertexIdx], avgOffset, 0.5f);
                    }
                }
            }

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var originalVertex = _originalVertices[i];
                var vertex = originalVertex;
                vertex.Position = originalVertex.Position + _vertexOffsets[i];
                mesh.Vertices[i] = vertex;
            }

            foreach (var triangle in mesh.Triangles)
            {
                for (int i = 0; i < triangle.TexCoordIndex.Length; i++)
                {
                    if (triangle.TexCoordIndex[i] < 0) continue;

                    int texCoordIdx = triangle.TexCoordIndex[i];
                    int vertexIdx = triangle.VertexIndex[i];

                    if (texCoordIdx >= 0 && texCoordIdx < mesh.TexCoords.Length &&
                        vertexIdx >= 0 && vertexIdx < mesh.Vertices.Length)
                    {
                        var originalTexCoord = _originalTexCoords[texCoordIdx];
                        var texCoord = originalTexCoord;

                        float uvOffset = _vertexOffsets[vertexIdx].Y / VERTEX_WAVE_HEIGHT * WAVE_AMPLITUDE;
                        texCoord.V = originalTexCoord.V + _currentOffset + uvOffset;
                        mesh.TexCoords[texCoordIdx] = texCoord;
                    }
                }
            }

            InvalidateBuffers();
        }

        public override void Draw(GameTime gameTime)
        {
            Alpha = 0.4f + (float)Math.Abs(Math.Sin(_waveTime * WAVE_FREQUENCY * 0.3f)) * 0.2f;
            base.Draw(gameTime);
        }
    }
}