using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Handles the rendering of terrain tiles, including opaque and alpha-blended layers.
    /// </summary>
    public class TerrainRenderer
    {
        private const float SpecialHeight = 1200f;
        private const int TileBatchVerts = 4096 * 6;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly TerrainData _data;
        private readonly TerrainVisibilityManager _visibility;
        private readonly TerrainLightManager _lightManager;
        private readonly GrassRenderer _grassRenderer;

        // Per-texture tile batches
        private readonly VertexPositionColorTexture[][] _tileBatches = new VertexPositionColorTexture[256][];
        private readonly int[] _tileBatchCounts = new int[256];
        private readonly VertexPositionColorTexture[][] _tileAlphaBatches = new VertexPositionColorTexture[256][];
        private readonly int[] _tileAlphaCounts = new int[256];

        // Buffers for a single terrain tile quad
        private readonly VertexPositionColorTexture[] _terrainVertices = new VertexPositionColorTexture[6];
        private readonly Vector2[] _terrainTextureCoords = new Vector2[4];
        private readonly Vector3[] _tempTerrainVertex = new Vector3[4];
        private readonly Color[] _tempTerrainLights = new Color[4];

        private Vector2 _waterFlowDir = Vector2.UnitX;
        private float _waterTotal = 0f;

        public float WaterSpeed { get; set; } = 0f;
        public float DistortionAmplitude { get; set; } = 0f;
        public float DistortionFrequency { get; set; } = 0f;
        public float AmbientLight { get; set; } = 0.25f;
        public short WorldIndex { get; set; }

        public int DrawCalls { get; private set; }
        public int DrawnTriangles { get; private set; }
        public int DrawnBlocks { get; private set; }
        public int DrawnCells { get; private set; }

        public Vector2 WaterFlowDirection
        {
            get => _waterFlowDir;
            set => _waterFlowDir = value.LengthSquared() < 1e-4f
                                   ? Vector2.UnitX
                                   : Vector2.Normalize(value);
        }

        public TerrainRenderer(GraphicsDevice graphicsDevice, TerrainData data, TerrainVisibilityManager visibility, TerrainLightManager lightManager, GrassRenderer grassRenderer)
        {
            _graphicsDevice = graphicsDevice;
            _data = data;
            _visibility = visibility;
            _lightManager = lightManager;
            _grassRenderer = grassRenderer;

            for (int i = 0; i < 256; i++)
            {
                _tileBatches[i] = new VertexPositionColorTexture[TileBatchVerts];
                _tileAlphaBatches[i] = new VertexPositionColorTexture[TileBatchVerts];
            }
        }

        public void CreateHeightMapTexture()
        {
            if (_data.HeightMap == null || _graphicsDevice == null) return;

            _data.HeightMapTexture = new Texture2D(_graphicsDevice, Constants.TERRAIN_SIZE, Constants.TERRAIN_SIZE, false, SurfaceFormat.Single);

            float[] heightData = ArrayPool<float>.Shared.Rent(Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE);
            try
            {
                for (int i = 0; i < heightData.Length; i++)
                {
                    heightData[i] = _data.HeightMap[i].B / 255.0f;
                }
                _data.HeightMapTexture.SetData(heightData);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(heightData);
            }
        }

        public void Update(GameTime time)
        {
            _waterTotal += (float)time.ElapsedGameTime.TotalSeconds * WaterSpeed;
        }

        public void Draw(bool after)
        {
            if (_graphicsDevice == null) return; // Added null check for _graphicsDevice
            if (_data.HeightMap == null) return;
            if (!after)
            {
                ResetMetrics();
                _grassRenderer.ResetMetrics();
            }

            var effect = GraphicsManager.Instance.BasicEffect3D;
            if (effect == null) return; // Check for effect

            if (Camera.Instance == null) return; // Added null check for Camera.Instance

            effect.Projection = Camera.Instance.Projection;
            effect.View = Camera.Instance.View;

            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block?.IsVisible == true)
                {
                    RenderTerrainBlock(
                        block.Xi, block.Yi,
                        after,
                        _visibility.LodSteps[block.LODLevel]);
                }
            }

            FlushAllTileBatches();
            _grassRenderer.Flush();
        }

        private void ResetMetrics()
        {
            DrawCalls = 0;
            DrawnTriangles = 0;
            DrawnBlocks = 0;
            DrawnCells = 0;
        }

        private bool IsPatchUniform(int xi, int yi, int size)
        {
            int idx0 = GetTerrainIndex(xi, yi);
            byte l1 = _data.Mapping.Layer1[idx0];
            byte l2 = _data.Mapping.Layer2[idx0];
            byte a0 = _data.Mapping.Alpha[idx0];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = GetTerrainIndex(xi + x, yi + y);
                    if (_data.Mapping.Layer1[idx] != l1
                     || _data.Mapping.Layer2[idx] != l2
                     || _data.Mapping.Alpha[idx] != a0)
                        return false;
                }
            }
            return true;
        }

        private void RenderTerrainBlock(int xi, int yi, bool after, int lodStep)
        {
            if (!after) DrawnBlocks++;

            if (lodStep > 1 && !IsPatchUniform(xi, yi, lodStep))
                lodStep = 1;

            _graphicsDevice.BlendState = BlendState.Opaque;

            for (int i = 0; i < 4; i += lodStep)
            {
                for (int j = 0; j < 4; j += lodStep)
                {
                    RenderTerrainTile(
                        xi + j, yi + i,
                        (float)lodStep, lodStep,
                        after);
                }
            }
        }

        private void RenderTerrainTile(int xi, int yi, float lodFactor, int lodInt, bool after)
        {
            if (after || _data.Attributes == null || _data.Attributes.TerrainWall == null) return; // Added null check for TerrainWall
            DrawnCells++;

            int i1 = GetTerrainIndex(xi, yi);
            if (_data.Attributes.TerrainWall[i1].HasFlag(Data.ATT.TWFlags.NoGround)) return;

            int i2 = GetTerrainIndex(xi + lodInt, yi);
            int i3 = GetTerrainIndex(xi + lodInt, yi + lodInt);
            int i4 = GetTerrainIndex(xi, yi + lodInt);

            PrepareTileVertices(xi, yi, i1, i2, i3, i4, lodFactor);
            PrepareTileLights(i1, i2, i3, i4);

            byte a1 = i1 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i1] : (byte)0;
            byte a2 = i2 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i2] : (byte)0;
            byte a3 = i3 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i3] : (byte)0;
            byte a4 = i4 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i4] : (byte)0;

            bool isOpaque = (a1 & a2 & a3 & a4) == 255;
            bool hasAlpha = (a1 | a2 | a3 | a4) != 0;

            if (isOpaque)
            {
                RenderTexture(_data.Mapping.Layer2[i1], xi, yi, lodFactor, useBatch: true, alphaLayer: false);
            }
            else
            {
                RenderTexture(_data.Mapping.Layer1[i1], xi, yi, lodFactor, useBatch: true, alphaLayer: false);
                if (hasAlpha)
                {
                    ApplyAlphaToLights(a1, a2, a3, a4);
                    RenderTexture(_data.Mapping.Layer2[i1], xi, yi, lodFactor, useBatch: true, alphaLayer: true);
                }
            }

            _grassRenderer.RenderGrassForTile(xi, yi, xi, yi, lodFactor, WorldIndex);
        }

        private void RenderTexture(int textureIndex, float xf, float yf, float lodScale, bool useBatch, bool alphaLayer = false)
        {
            if (textureIndex < 0 || textureIndex >= _data.Textures.Length || _data.Textures[textureIndex] == null)
                return;

            var texture = _data.Textures[textureIndex];
            float baseW = 64f / texture.Width;
            float baseH = 64f / texture.Height;
            float suf = xf * baseW;
            float svf = yf * baseH;
            float uvW = baseW * lodScale;
            float uvH = baseH * lodScale;

            if (textureIndex == 5) // Water
            {
                var flowOff = _waterFlowDir * _waterTotal;
                float wrapPeriod = 2 * (float)Math.PI / Math.Max(0.01f, DistortionFrequency);
                float waterPhase = _waterTotal % wrapPeriod;

                _terrainTextureCoords[0].X = suf + flowOff.X + (float)Math.Sin((suf + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[0].Y = svf + flowOff.Y + (float)Math.Cos((svf + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[1].X = suf + uvW + flowOff.X + (float)Math.Sin((suf + uvW + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[1].Y = svf + flowOff.Y + (float)Math.Cos((svf + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[2].X = _terrainTextureCoords[1].X;
                _terrainTextureCoords[2].Y = svf + uvH + flowOff.Y + (float)Math.Cos((svf + uvH + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[3].X = _terrainTextureCoords[0].X;
                _terrainTextureCoords[3].Y = _terrainTextureCoords[2].Y;
            }
            else
            {
                _terrainTextureCoords[0] = new Vector2(suf, svf);
                _terrainTextureCoords[1] = new Vector2(suf + uvW, svf);
                _terrainTextureCoords[2] = new Vector2(suf + uvW, svf + uvH);
                _terrainTextureCoords[3] = new Vector2(suf, svf + uvH);
            }

            _terrainVertices[0] = new VertexPositionColorTexture(_tempTerrainVertex[0], _tempTerrainLights[0], _terrainTextureCoords[0]);
            _terrainVertices[1] = new VertexPositionColorTexture(_tempTerrainVertex[1], _tempTerrainLights[1], _terrainTextureCoords[1]);
            _terrainVertices[2] = new VertexPositionColorTexture(_tempTerrainVertex[2], _tempTerrainLights[2], _terrainTextureCoords[2]);
            _terrainVertices[3] = new VertexPositionColorTexture(_tempTerrainVertex[2], _tempTerrainLights[2], _terrainTextureCoords[2]);
            _terrainVertices[4] = new VertexPositionColorTexture(_tempTerrainVertex[3], _tempTerrainLights[3], _terrainTextureCoords[3]);
            _terrainVertices[5] = new VertexPositionColorTexture(_tempTerrainVertex[0], _tempTerrainLights[0], _terrainTextureCoords[0]);

            if (useBatch)
            {
                AddTileToBatch(textureIndex, _terrainVertices, alphaLayer);
            }
            else
            {
                var basicEffect = GraphicsManager.Instance.BasicEffect3D;
                basicEffect.Texture = texture;
                foreach (var pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, 2);
                }
                DrawCalls++;
                DrawnTriangles += 2;
            }
        }

        private void PrepareTileVertices(int xi, int yi, int i1, int i2, int i3, int i4, float lodFactor)
        {
            float h1 = i1 < _data.HeightMap.Length ? _data.HeightMap[i1].B * 1.5f : 0f;
            float h2 = i2 < _data.HeightMap.Length ? _data.HeightMap[i2].B * 1.5f : 0f;
            float h3 = i3 < _data.HeightMap.Length ? _data.HeightMap[i3].B * 1.5f : 0f;
            float h4 = i4 < _data.HeightMap.Length ? _data.HeightMap[i4].B * 1.5f : 0f;

            float sx = xi * Constants.TERRAIN_SCALE;
            float sy = yi * Constants.TERRAIN_SCALE;
            float ss = Constants.TERRAIN_SCALE * lodFactor;

            _tempTerrainVertex[0] = new Vector3(sx, sy, h1);
            _tempTerrainVertex[1] = new Vector3(sx + ss, sy, h2);
            _tempTerrainVertex[2] = new Vector3(sx + ss, sy + ss, h3);
            _tempTerrainVertex[3] = new Vector3(sx, sy + ss, h4);

            if (i1 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i1].HasFlag(Data.ATT.TWFlags.Height))
                _tempTerrainVertex[0].Z += SpecialHeight;
            if (i2 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i2].HasFlag(Data.ATT.TWFlags.Height))
                _tempTerrainVertex[1].Z += SpecialHeight;
            if (i3 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i3].HasFlag(Data.ATT.TWFlags.Height))
                _tempTerrainVertex[2].Z += SpecialHeight;
            if (i4 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i4].HasFlag(Data.ATT.TWFlags.Height))
                _tempTerrainVertex[3].Z += SpecialHeight;
        }

        private void PrepareTileLights(int i1, int i2, int i3, int i4)
        {
            _tempTerrainLights[0] = BuildVertexLight(i1, _tempTerrainVertex[0]);
            _tempTerrainLights[1] = BuildVertexLight(i2, _tempTerrainVertex[1]);
            _tempTerrainLights[2] = BuildVertexLight(i3, _tempTerrainVertex[2]);
            _tempTerrainLights[3] = BuildVertexLight(i4, _tempTerrainVertex[3]);
        }

        private Color BuildVertexLight(int index, Vector3 pos)
        {
            if (_data.FinalLightMap == null) return Color.White; // Added null check for _data.FinalLightMap

            Vector3 baseColor = index < _data.FinalLightMap.Length
                ? new Vector3(_data.FinalLightMap[index].R, _data.FinalLightMap[index].G, _data.FinalLightMap[index].B)
                : Vector3.Zero;

            baseColor += new Vector3(AmbientLight * 255f);
            baseColor += _lightManager.EvaluateDynamicLight(new Vector2(pos.X, pos.Y));
            baseColor = Vector3.Clamp(baseColor, Vector3.Zero, new Vector3(255f));

            return new Color((int)baseColor.X, (int)baseColor.Y, (int)baseColor.Z);
        }

        private void ApplyAlphaToLights(byte a1, byte a2, byte a3, byte a4)
        {
            _tempTerrainLights[0] *= a1 / 255f; _tempTerrainLights[0].A = a1;
            _tempTerrainLights[1] *= a2 / 255f; _tempTerrainLights[1].A = a2;
            _tempTerrainLights[2] *= a3 / 255f; _tempTerrainLights[2].A = a3;
            _tempTerrainLights[3] *= a4 / 255f; _tempTerrainLights[3].A = a4;
        }

        private void AddTileToBatch(int texIndex, VertexPositionColorTexture[] verts, bool alphaLayer)
        {
            var batch = alphaLayer ? _tileAlphaBatches[texIndex] : _tileBatches[texIndex];
            var counters = alphaLayer ? _tileAlphaCounts : _tileBatchCounts;

            int dstOff = counters[texIndex];
            if (dstOff + 6 > TileBatchVerts)
            {
                FlushSingleTexture(texIndex, alphaLayer);
                dstOff = 0;
            }

            Array.Copy(verts, 0, batch, dstOff, 6);
            counters[texIndex] = dstOff + 6;
        }

        private void FlushSingleTexture(int texIndex, bool alphaLayer)
        {
            int vertCount = alphaLayer ? _tileAlphaCounts[texIndex] : _tileBatchCounts[texIndex];
            if (vertCount == 0) return;

            var batch = alphaLayer ? _tileAlphaBatches[texIndex] : _tileBatches[texIndex];
            var effect = GraphicsManager.Instance.BasicEffect3D;
            if (effect == null || effect.CurrentTechnique == null) return; // Added null checks for effect and effect.CurrentTechnique
            if (_data.Textures[texIndex] == null) return; // Added null check for texture

            effect.Texture = _data.Textures[texIndex];
            _graphicsDevice.BlendState = alphaLayer ? BlendState.AlphaBlend : BlendState.Opaque;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    batch, 0,
                    vertCount / 3);
            }

            DrawCalls++;
            DrawnTriangles += vertCount / 3;

            if (alphaLayer) _tileAlphaCounts[texIndex] = 0;
            else _tileBatchCounts[texIndex] = 0;
        }

        private void FlushAllTileBatches()
        {
            for (int t = 0; t < 256; t++)
            {
                if (_tileBatchCounts[t] > 0)
                    FlushSingleTexture(t, alphaLayer: false);
            }
            for (int t = 0; t < 256; t++)
            {
                if (_tileAlphaCounts[t] > 0)
                    FlushSingleTexture(t, alphaLayer: true);
            }
            _graphicsDevice.BlendState = BlendState.Opaque;
        }

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;
    }
}
