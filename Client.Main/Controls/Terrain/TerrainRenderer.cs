using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Handles the rendering of terrain tiles, including opaque and alpha-blended layers.
    /// </summary>
    public class TerrainRenderer
    {
        private const float SpecialHeight = 1200f;
        private const int BlockSize = 4;
        private const int TileBatchVerts = 16384 * 6;
        private const int TileBatchIndices = 16384 * 6;
        private const float LodSkirtDepth = Constants.TERRAIN_SCALE * 1.5f;

        private const byte LodEdgeNorth = 1;
        private const byte LodEdgeSouth = 2;
        private const byte LodEdgeWest = 4;
        private const byte LodEdgeEast = 8;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly TerrainData _data;
        private readonly TerrainVisibilityManager _visibility;
        private readonly TerrainLightManager _lightManager;
        private readonly GrassRenderer _grassRenderer;

        // Per-texture tile batches
        private readonly TerrainVertexPositionColorNormalTexture[][] _tileBatches = new TerrainVertexPositionColorNormalTexture[256][];
        private readonly int[] _tileBatchCounts = new int[256];
        private readonly TerrainVertexPositionColorNormalTexture[][] _tileAlphaBatches = new TerrainVertexPositionColorNormalTexture[256][];
        private readonly int[] _tileAlphaCounts = new int[256];

        // Per-texture tile index batches (used when terrain can be rendered from a static VB)
        private readonly ushort[][] _tileIndexBatches = new ushort[256][];
        private readonly int[] _tileIndexCounts = new int[256];
        private readonly ushort[][] _tileAlphaIndexBatches = new ushort[256][];
        private readonly int[] _tileAlphaIndexCounts = new int[256];

        // Buffers for a single terrain tile quad
        private readonly TerrainVertexPositionColorNormalTexture[] _terrainVertices = new TerrainVertexPositionColorNormalTexture[6];
        private readonly Vector2[] _terrainTextureCoords = new Vector2[4];
        private readonly Vector3[] _tempTerrainVertex = new Vector3[4];
        private readonly Vector3[] _tempTerrainNormals = new Vector3[4];
        private readonly Color[] _tempTerrainLights = new Color[4];
        private readonly Color[] _tempTerrainLightsBase = new Color[4];
        private readonly VertexPositionColorTexture[] _fallbackTileBuffer = new VertexPositionColorTexture[TileBatchVerts];

        // Cached per-vertex data (built once per terrain) to avoid per-tile CPU work each frame.
        private bool _vertexCacheBuilt;
        private Color[] _cachedHeightMapRef;
        private Client.Data.ATT.TWFlags[] _cachedTerrainWallRef;
        private Vector3[] _cachedNormalsRef;
        private Vector3[] _cachedVertexPositions;
        private Vector3[] _cachedVertexNormals;

        private bool _lightCacheBuilt;
        private Color[] _cachedFinalLightMapRef;
        private float _cachedAmbientLight = float.NaN;
        private Color[] _cachedVertexBaseLights;

        // State tracking for GPU optimization
        private Texture2D _lastBoundTexture = null;
        private BlendState _lastBlendState = null;
        private bool _useDynamicLightingShader = false;
        private bool _useTerrainIndexBatching = false;

        // Static terrain vertex buffers (terrain uses procedural UVs in the shader)
        private VertexBuffer _terrainVertexBufferBase;
        private VertexBuffer _terrainVertexBufferAlpha;
        private bool _terrainVertexBuffersBuilt;
        private Color[] _terrainBuffersHeightMapRef;
        private Client.Data.ATT.TWFlags[] _terrainBuffersTerrainWallRef;
        private Vector3[] _terrainBuffersNormalsRef;
        private Color[] _terrainBuffersFinalLightMapRef;
        private float _terrainBuffersAmbientLight = float.NaN;
        private byte[] _terrainBuffersAlphaMapRef;

        private readonly Vector3[] _cachedLightPositions = new Vector3[16];
        private readonly Vector3[] _cachedLightColors = new Vector3[16];
        private readonly float[] _cachedLightRadii = new float[16];
        private readonly float[] _cachedLightIntensities = new float[16];
        private readonly float[] _cachedLightScores = new float[16];
        private const float MinLightInfluence = 0.001f;
        private int _cachedSelectedLightsVersion = -1;
        private int _cachedSelectedLightsMax = -1;
        private int _cachedSelectedLightCount = 0;

        private Vector2 _waterFlowDir = Vector2.UnitX;
        private float _waterTotal = 0f;

        // Precomputed UV scales to avoid divisions per tile
        private readonly Vector2[] _tileUvScale = new Vector2[256];
        private readonly Vector2[] _tileUvScaleWorld = new Vector2[256];

        private readonly int _blocksPerSide;
        private readonly sbyte[] _visibleBlockLod;
        private bool _hasLodTransitions;

        public float WaterSpeed { get; set; } = 0f;
        public float DistortionAmplitude { get; set; } = 0f;
        public float DistortionFrequency { get; set; } = 0f;
        public float AmbientLight { get; set; } = 0.25f;
        public short WorldIndex { get; set; }

        public int DrawCalls { get; private set; }
        public int DrawnTriangles { get; private set; }
        public int DrawnBlocks { get; private set; }
        public int DrawnCells { get; private set; }
        public bool IsGpuLightingActive => _useDynamicLightingShader;
        public bool IsDynamicLightingShaderAvailable => GraphicsManager.Instance.DynamicLightingEffect != null;

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
            _blocksPerSide = Constants.TERRAIN_SIZE / BlockSize;
            _visibleBlockLod = new sbyte[_blocksPerSide * _blocksPerSide];

            // Precompute UV scales for all textures
            PrecomputeUVScales();
        }

        private void PrecomputeUVScales()
        {
            for (int t = 0; t < _data.Textures.Length; t++)
            {
                if (_data.Textures[t] != null)
                {
                    float invW = 64f / _data.Textures[t].Width;
                    float invH = 64f / _data.Textures[t].Height;
                    _tileUvScale[t] = new Vector2(invW, invH);
                    _tileUvScaleWorld[t] = new Vector2(invW / Constants.TERRAIN_SCALE, invH / Constants.TERRAIN_SCALE);
                }
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
                    heightData[i] = _data.HeightMap[i].R / 255.0f;
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

        private void EnsureVertexCache()
        {
            if (_data.HeightMap == null)
                return;

            int total = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE;
            var heightMap = _data.HeightMap;
            var terrainWall = _data.Attributes?.TerrainWall;
            var normals = _data.Normals;

            if (_vertexCacheBuilt &&
                _cachedVertexPositions != null &&
                _cachedVertexPositions.Length == total &&
                ReferenceEquals(heightMap, _cachedHeightMapRef) &&
                ReferenceEquals(terrainWall, _cachedTerrainWallRef) &&
                ReferenceEquals(normals, _cachedNormalsRef))
            {
                return;
            }

            _cachedVertexPositions ??= new Vector3[total];
            if (_cachedVertexPositions.Length != total)
                _cachedVertexPositions = new Vector3[total];

            _cachedVertexNormals ??= new Vector3[total];
            if (_cachedVertexNormals.Length != total)
                _cachedVertexNormals = new Vector3[total];

            for (int i = 0; i < total; i++)
            {
                int x = i % Constants.TERRAIN_SIZE;
                int y = i / Constants.TERRAIN_SIZE;

                float z = heightMap[i].R * 1.5f;
                if (terrainWall != null && (terrainWall[i] & Client.Data.ATT.TWFlags.Height) != 0)
                    z += SpecialHeight;

                _cachedVertexPositions[i] = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, z);

                if (normals == null || (uint)i >= (uint)normals.Length)
                {
                    _cachedVertexNormals[i] = Vector3.UnitZ;
                }
                else
                {
                    var n = normals[i];
                    _cachedVertexNormals[i] = n.LengthSquared() < 1e-6f ? Vector3.UnitZ : n;
                }
            }

            _cachedHeightMapRef = heightMap;
            _cachedTerrainWallRef = terrainWall;
            _cachedNormalsRef = normals;
            _vertexCacheBuilt = true;
        }

        private void EnsureLightCache()
        {
            int total = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE;
            var finalLightMap = _data.FinalLightMap;

            if (_lightCacheBuilt &&
                _cachedVertexBaseLights != null &&
                _cachedVertexBaseLights.Length == total &&
                ReferenceEquals(finalLightMap, _cachedFinalLightMapRef) &&
                Math.Abs(_cachedAmbientLight - AmbientLight) < 0.0001f)
            {
                return;
            }

            _cachedVertexBaseLights ??= new Color[total];
            if (_cachedVertexBaseLights.Length != total)
                _cachedVertexBaseLights = new Color[total];

            if (finalLightMap == null || finalLightMap.Length < total)
            {
                // Match old behavior: if FinalLightMap is missing, BuildVertexLight returned Color.White (no ambient).
                for (int i = 0; i < total; i++)
                    _cachedVertexBaseLights[i] = Color.White;

                // Ambient is ignored in this fallback, but track it to avoid rebuilding every frame.
                _cachedAmbientLight = AmbientLight;
            }
            else
            {
                float ambient = AmbientLight * 255f;
                for (int i = 0; i < total; i++)
                {
                    var c = finalLightMap[i];
                    float r = MathF.Min(c.R + ambient, 255f);
                    float g = MathF.Min(c.G + ambient, 255f);
                    float b = MathF.Min(c.B + ambient, 255f);
                    _cachedVertexBaseLights[i] = new Color((byte)r, (byte)g, (byte)b, (byte)255);
                }

                _cachedAmbientLight = AmbientLight;
            }

            _cachedFinalLightMapRef = finalLightMap;
            _lightCacheBuilt = true;
        }

        public void Draw(bool after)
        {
            if (_graphicsDevice == null) return; // Added null check for _graphicsDevice
            if (_data.HeightMap == null) return;
            if (Camera.Instance == null) return; // Added null check for Camera.Instance
            if (!after)
            {
                ResetMetrics();
                _grassRenderer.ResetMetrics();
            }

            _useDynamicLightingShader = Constants.ENABLE_TERRAIN_GPU_LIGHTING &&
                                        Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                        GraphicsManager.Instance.DynamicLightingEffect != null;
            _useTerrainIndexBatching = false;

            if (!after)
            {
                EnsureVertexCache();
                EnsureLightCache();
                _hasLodTransitions = BuildVisibleLodGrid();
            }
            else
            {
                _hasLodTransitions = false;
            }

            if (_useDynamicLightingShader)
            {
                var effect = GraphicsManager.Instance.DynamicLightingEffect;

                // Enable index batching only when the shader supports procedural terrain UVs.
                if (!after && !_hasLodTransitions && SupportsProceduralTerrainUv(effect))
                {
                    EnsureTerrainVertexBuffers();
                    _useTerrainIndexBatching = _terrainVertexBufferBase != null &&
                                               !_terrainVertexBufferBase.IsDisposed &&
                                               _terrainVertexBufferAlpha != null &&
                                               !_terrainVertexBufferAlpha.IsDisposed;
                }

                if (_useTerrainIndexBatching && VisibleBlocksTouchTerrainEdge())
                    _useTerrainIndexBatching = false;

                if (effect != null)
                {
                    effect.CurrentTechnique = effect.Techniques["DynamicLighting"];
                }
                if (!ConfigureDynamicLightingEffect())
                    return;
            }
            else
            {
                var effect = GraphicsManager.Instance.BasicEffect3D;
                if (effect == null) return; // Check for effect

                effect.Projection = Camera.Instance.Projection;
                effect.View = Camera.Instance.View;
                effect.World = Matrix.Identity;
                // Configure alpha blending to match custom shader behavior
                effect.Alpha = 1.0f;
                effect.VertexColorEnabled = true;
            }

            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block?.IsVisible == true)
                {
                    RenderTerrainBlock(
                        block.Xi, block.Yi,
                        after,
                        _visibility.LodSteps[block.LODLevel],
                        block); // Pass block for hierarchical culling
                }
            }

            if (_useTerrainIndexBatching)
                FlushAllTileIndexBatches();
            else
                FlushAllTileBatches();
            _grassRenderer.Flush();
        }

        private void ResetMetrics()
        {
            DrawCalls = 0;
            DrawnTriangles = 0;
            DrawnBlocks = 0;
            DrawnCells = 0;

            // Reset state tracking for new frame
            _lastBoundTexture = null;
            _lastBlendState = null;
        }

        private bool BuildVisibleLodGrid()
        {
            if (_visibility?.VisibleBlocks == null || _visibleBlockLod == null || _visibleBlockLod.Length == 0)
                return false;

            Array.Fill(_visibleBlockLod, (sbyte)-1);

            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block == null || !block.IsVisible)
                    continue;

                int bx = block.Xi / BlockSize;
                int by = block.Yi / BlockSize;
                if ((uint)bx >= (uint)_blocksPerSide || (uint)by >= (uint)_blocksPerSide)
                    continue;

                int lodStep = _visibility.LodSteps[block.LODLevel];
                _visibleBlockLod[by * _blocksPerSide + bx] = (sbyte)lodStep;
            }

            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block == null || !block.IsVisible)
                    continue;

                int lodStep = _visibility.LodSteps[block.LODLevel];
                if (lodStep <= 1)
                    continue;

                int bx = block.Xi / BlockSize;
                int by = block.Yi / BlockSize;
                if (HasLowerLodNeighbor(bx, by, lodStep))
                    return true;
            }

            return false;
        }

        private bool VisibleBlocksTouchTerrainEdge()
        {
            if (_visibility?.VisibleBlocks == null)
                return false;

            int edgeStart = Constants.TERRAIN_SIZE - BlockSize;
            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block?.IsVisible != true)
                    continue;

                if (block.Xi >= edgeStart || block.Yi >= edgeStart)
                    return true;
            }

            return false;
        }

        private bool HasLowerLodNeighbor(int bx, int by, int lodStep)
        {
            return IsLowerLodNeighbor(bx, by - 1, lodStep) ||
                   IsLowerLodNeighbor(bx, by + 1, lodStep) ||
                   IsLowerLodNeighbor(bx - 1, by, lodStep) ||
                   IsLowerLodNeighbor(bx + 1, by, lodStep);
        }

        private bool IsLowerLodNeighbor(int bx, int by, int lodStep)
        {
            if ((uint)bx >= (uint)_blocksPerSide || (uint)by >= (uint)_blocksPerSide)
                return false;

            int neighbor = _visibleBlockLod[by * _blocksPerSide + bx];
            return neighbor > 0 && neighbor < lodStep;
        }

        private byte GetLodEdgeMask(TerrainBlock block, int lodStep)
        {
            if (block == null || lodStep <= 1 || !_hasLodTransitions)
                return 0;

            int bx = block.Xi / BlockSize;
            int by = block.Yi / BlockSize;
            byte mask = 0;

            if (IsLowerLodNeighbor(bx, by - 1, lodStep)) mask |= LodEdgeNorth;
            if (IsLowerLodNeighbor(bx, by + 1, lodStep)) mask |= LodEdgeSouth;
            if (IsLowerLodNeighbor(bx - 1, by, lodStep)) mask |= LodEdgeWest;
            if (IsLowerLodNeighbor(bx + 1, by, lodStep)) mask |= LodEdgeEast;

            return mask;
        }

        public void DrawShadowMap(Effect shadowEffect, Matrix lightViewProjection)
        {
            if (_graphicsDevice == null || _data.HeightMap == null || shadowEffect == null)
                return;

            EnsureVertexCache();
            EnsureLightCache();

            var prevBlend = _graphicsDevice.BlendState;
            var prevDepth = _graphicsDevice.DepthStencilState;
            var prevRaster = _graphicsDevice.RasterizerState;
            var prevTechnique = shadowEffect.CurrentTechnique;
            var prevUseDynamic = _useDynamicLightingShader;
            var prevUseIndex = _useTerrainIndexBatching;
            var prevLastBoundTexture = _lastBoundTexture;

            // Reset texture binding to ensure correct textures are bound for shadow pass
            _lastBoundTexture = null;
            _useDynamicLightingShader = true;
            _useTerrainIndexBatching = false;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            shadowEffect.CurrentTechnique = shadowEffect.Techniques["ShadowCaster"];
            shadowEffect.Parameters["World"]?.SetValue(Matrix.Identity);
            shadowEffect.Parameters["LightViewProjection"]?.SetValue(lightViewProjection);
            shadowEffect.Parameters["ShadowMapTexelSize"]?.SetValue(new Vector2(1f / Constants.SHADOW_MAP_SIZE, 1f / Constants.SHADOW_MAP_SIZE));
            shadowEffect.Parameters["ShadowBias"]?.SetValue(Constants.SHADOW_BIAS);
            shadowEffect.Parameters["ShadowNormalBias"]?.SetValue(Constants.SHADOW_NORMAL_BIAS);
            shadowEffect.Parameters["Alpha"]?.SetValue(1f);

            // Optional fast path for terrain: static VB + per-texture index batching (requires shader support).
            _hasLodTransitions = BuildVisibleLodGrid();
            if (!_hasLodTransitions && SupportsProceduralTerrainUv(shadowEffect))
            {
                EnsureTerrainVertexBuffers();
                _useTerrainIndexBatching = _terrainVertexBufferBase != null &&
                                           !_terrainVertexBufferBase.IsDisposed &&
                                           _terrainVertexBufferAlpha != null &&
                                           !_terrainVertexBufferAlpha.IsDisposed;
            }

            if (_useTerrainIndexBatching && VisibleBlocksTouchTerrainEdge())
                _useTerrainIndexBatching = false;

            shadowEffect.Parameters["UseProceduralTerrainUV"]?.SetValue(_useTerrainIndexBatching ? 1.0f : 0.0f);
            shadowEffect.Parameters["IsWaterTexture"]?.SetValue(0.0f);
            shadowEffect.Parameters["TerrainUvScale"]?.SetValue(Vector2.Zero);
            shadowEffect.Parameters["WaterFlowDirection"]?.SetValue(_waterFlowDir);
            shadowEffect.Parameters["WaterTotal"]?.SetValue(_waterTotal);
            shadowEffect.Parameters["DistortionAmplitude"]?.SetValue(DistortionAmplitude);
            shadowEffect.Parameters["DistortionFrequency"]?.SetValue(DistortionFrequency);

            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block?.IsVisible == true)
                {
                    RenderTerrainBlock(
                        block.Xi, block.Yi,
                        after: false,
                        _visibility.LodSteps[block.LODLevel],
                        block);
                }
            }

            if (_useTerrainIndexBatching)
                FlushAllTileIndexBatches();
            else
                FlushAllTileBatches();

            // Restore for object shadow casters (ShadowVS uses this uniform too).
            shadowEffect.Parameters["UseProceduralTerrainUV"]?.SetValue(0.0f);
            shadowEffect.Parameters["IsWaterTexture"]?.SetValue(0.0f);

            _useDynamicLightingShader = prevUseDynamic;
            _useTerrainIndexBatching = prevUseIndex;
            _lastBoundTexture = prevLastBoundTexture;
            _graphicsDevice.BlendState = prevBlend;
            _graphicsDevice.DepthStencilState = prevDepth;
            _graphicsDevice.RasterizerState = prevRaster;
            shadowEffect.CurrentTechnique = prevTechnique;
        }

        private bool ConfigureDynamicLightingEffect()
        {
            var effect = GraphicsManager.Instance.DynamicLightingEffect;
            if (effect == null || Camera.Instance == null)
                return false;

            Matrix world = Matrix.Identity;
            effect.Parameters["World"]?.SetValue(world);
            effect.Parameters["View"]?.SetValue(Camera.Instance.View);
            effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(world * Camera.Instance.View * Camera.Instance.Projection);
            effect.Parameters["EyePosition"]?.SetValue(Camera.Instance.Position);
            // Use terrain technique instead of setting uniforms (better performance, no shader branches)
            effect.CurrentTechnique = effect.Techniques["DynamicLighting_Terrain"];
            effect.Parameters["UseProceduralTerrainUV"]?.SetValue(_useTerrainIndexBatching ? 1.0f : 0.0f);
            effect.Parameters["IsWaterTexture"]?.SetValue(0.0f);
            effect.Parameters["TerrainUvScale"]?.SetValue(Vector2.Zero);
            effect.Parameters["WaterFlowDirection"]?.SetValue(_waterFlowDir);
            effect.Parameters["WaterTotal"]?.SetValue(_waterTotal);
            effect.Parameters["DistortionAmplitude"]?.SetValue(DistortionAmplitude);
            effect.Parameters["DistortionFrequency"]?.SetValue(DistortionFrequency);
            effect.Parameters["TerrainDynamicIntensityScale"]?.SetValue(1.0f);
            effect.Parameters["Alpha"]?.SetValue(1f);
            effect.Parameters["DebugLightingAreas"]?.SetValue(Constants.DEBUG_LIGHTING_AREAS ? 1.0f : 0.0f);

            // Ambient and sun are ignored when using baked vertex lighting, but keep sane defaults.
            float ambientValue = AmbientLight * SunCycleManager.AmbientMultiplier;
            effect.Parameters["AmbientLight"]?.SetValue(new Vector3(ambientValue));
            // GlobalLightMultiplier dims vertex color lighting for day-night cycle
            effect.Parameters["GlobalLightMultiplier"]?.SetValue(SunCycleManager.AmbientMultiplier);
            Vector3 sunDir = GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION;
            if (sunDir.LengthSquared() < 0.0001f)
                sunDir = new Vector3(1f, 0f, -0.6f);
            sunDir = Vector3.Normalize(sunDir);
            bool sunEnabled = Constants.SUN_ENABLED;
            effect.Parameters["SunDirection"]?.SetValue(sunDir);
            effect.Parameters["SunColor"]?.SetValue(new Vector3(1f, 0.95f, 0.85f));
            effect.Parameters["SunStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveSunStrength() : 0f);
            effect.Parameters["ShadowStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveShadowStrength() : 0f);

            // Apply global shadow map parameters when available so terrain receives the same shadows as objects
            GraphicsManager.Instance.ShadowMapRenderer?.ApplyShadowParameters(effect);
            UploadDynamicLights(effect);
            return true;
        }

        private void UploadDynamicLights(Effect effect)
        {
            if (!Constants.ENABLE_DYNAMIC_LIGHTS)
            {
                effect.Parameters["ActiveLightCount"]?.SetValue(0);
                effect.Parameters["MaxLightsToProcess"]?.SetValue(0);
                return;
            }

            var activeLights = _lightManager.ActiveLights;
            int maxLights = Constants.OPTIMIZE_FOR_INTEGRATED_GPU ? 4 : 16;
            int count = 0;
            int version = _lightManager.ActiveLightsVersion;

            if (activeLights != null && activeLights.Count > 0 && Camera.Instance != null)
            {
                if (_cachedSelectedLightsVersion != version || _cachedSelectedLightsMax != maxLights)
                {
                    if (TryGetVisibleTerrainBounds(out Vector2 visibleMin, out Vector2 visibleMax))
                    {
                        _cachedSelectedLightCount = SelectRelevantLights(activeLights, visibleMin, visibleMax, maxLights);
                    }
                    else
                    {
                        // Fallback: select around what the camera is looking at (target).
                        var camTarget = Camera.Instance.Target;
                        var referencePos = new Vector2(camTarget.X, camTarget.Y);
                        _cachedSelectedLightCount = SelectRelevantLights(activeLights, referencePos, maxLights);
                    }

                    _cachedSelectedLightsVersion = version;
                    _cachedSelectedLightsMax = maxLights;
                }

                count = _cachedSelectedLightCount;
            }
            else
            {
                _cachedSelectedLightCount = 0;
                _cachedSelectedLightsVersion = version;
                _cachedSelectedLightsMax = maxLights;
            }

            effect.Parameters["ActiveLightCount"]?.SetValue(count);
            effect.Parameters["MaxLightsToProcess"]?.SetValue(maxLights);
            if (count > 0)
            {
                effect.Parameters["LightPositions"]?.SetValue(_cachedLightPositions);
                effect.Parameters["LightColors"]?.SetValue(_cachedLightColors);
                effect.Parameters["LightRadii"]?.SetValue(_cachedLightRadii);
                effect.Parameters["LightIntensities"]?.SetValue(_cachedLightIntensities);
            }
        }

        private bool TryGetVisibleTerrainBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            if (_visibility?.VisibleBlocks == null || _visibility.VisibleBlocks.Count == 0)
                return false;

            bool any = false;
            foreach (var block in _visibility.VisibleBlocks)
            {
                if (block == null)
                    continue;

                var bmin = block.Bounds.Min;
                var bmax = block.Bounds.Max;

                if (bmin.X < min.X) min.X = bmin.X;
                if (bmin.Y < min.Y) min.Y = bmin.Y;
                if (bmax.X > max.X) max.X = bmax.X;
                if (bmax.Y > max.Y) max.Y = bmax.Y;

                any = true;
            }

            return any;
        }

        private static float DistanceSquaredPointToRect(Vector2 p, Vector2 min, Vector2 max)
        {
            float cx = p.X < min.X ? min.X : (p.X > max.X ? max.X : p.X);
            float cy = p.Y < min.Y ? min.Y : (p.Y > max.Y ? max.Y : p.Y);

            float dx = p.X - cx;
            float dy = p.Y - cy;
            return dx * dx + dy * dy;
        }

        private int SelectRelevantLights(IReadOnlyList<DynamicLightSnapshot> activeLights, Vector2 regionMin, Vector2 regionMax, int maxLights)
        {
            maxLights = Math.Min(maxLights, _cachedLightPositions.Length);
            if (activeLights == null || activeLights.Count == 0 || maxLights <= 0)
                return 0;

            int selected = 0;
            float weakestScore = float.MaxValue;
            int weakestIndex = 0;

            for (int i = 0; i < activeLights.Count; i++)
            {
                var light = activeLights[i];
                float radius = light.Radius;
                float radiusSq = radius * radius;
                if (radiusSq <= 0.001f) continue;

                var lightPos2 = new Vector2(light.Position.X, light.Position.Y);
                float distSq = DistanceSquaredPointToRect(lightPos2, regionMin, regionMax);
                if (distSq >= radiusSq)
                    continue;

                float influence = (1f - distSq / radiusSq) * light.Intensity;
                if (influence <= MinLightInfluence)
                    continue;

                if (selected < maxLights)
                {
                    _cachedLightScores[selected] = influence;
                    _cachedLightPositions[selected] = light.Position;
                    _cachedLightColors[selected] = light.Color;
                    _cachedLightRadii[selected] = radius;
                    _cachedLightIntensities[selected] = light.Intensity;

                    if (influence < weakestScore)
                    {
                        weakestScore = influence;
                        weakestIndex = selected;
                    }

                    selected++;
                }
                else if (influence > weakestScore)
                {
                    _cachedLightScores[weakestIndex] = influence;
                    _cachedLightPositions[weakestIndex] = light.Position;
                    _cachedLightColors[weakestIndex] = light.Color;
                    _cachedLightRadii[weakestIndex] = radius;
                    _cachedLightIntensities[weakestIndex] = light.Intensity;

                    weakestScore = _cachedLightScores[0];
                    weakestIndex = 0;
                    for (int j = 1; j < selected; j++)
                    {
                        float score = _cachedLightScores[j];
                        if (score < weakestScore)
                        {
                            weakestScore = score;
                            weakestIndex = j;
                        }
                    }
                }
            }

            return selected;
        }

        private int SelectRelevantLights(IReadOnlyList<DynamicLightSnapshot> activeLights, Vector2 referencePos, int maxLights)
        {
            maxLights = Math.Min(maxLights, _cachedLightPositions.Length);
            if (activeLights == null || activeLights.Count == 0 || maxLights <= 0)
                return 0;

            int selected = 0;
            float weakestScore = float.MaxValue;
            int weakestIndex = 0;

            for (int i = 0; i < activeLights.Count; i++)
            {
                var light = activeLights[i];
                float radius = light.Radius;
                float radiusSq = radius * radius;

                var diff = new Vector2(light.Position.X, light.Position.Y) - referencePos;
                float distSq = diff.LengthSquared();
                if (distSq >= radiusSq)
                    continue;

                float influence = (1f - distSq / radiusSq) * light.Intensity;
                if (influence <= MinLightInfluence)
                    continue;

                if (selected < maxLights)
                {
                    _cachedLightScores[selected] = influence;
                    _cachedLightPositions[selected] = light.Position;
                    _cachedLightColors[selected] = light.Color;
                    _cachedLightRadii[selected] = radius;
                    _cachedLightIntensities[selected] = light.Intensity;

                    if (influence < weakestScore)
                    {
                        weakestScore = influence;
                        weakestIndex = selected;
                    }

                    selected++;
                }
                else if (influence > weakestScore)
                {
                    _cachedLightScores[weakestIndex] = influence;
                    _cachedLightPositions[weakestIndex] = light.Position;
                    _cachedLightColors[weakestIndex] = light.Color;
                    _cachedLightRadii[weakestIndex] = radius;
                    _cachedLightIntensities[weakestIndex] = light.Intensity;

                    weakestScore = _cachedLightScores[0];
                    weakestIndex = 0;
                    for (int j = 1; j < selected; j++)
                    {
                        float score = _cachedLightScores[j];
                        if (score < weakestScore)
                        {
                            weakestScore = score;
                            weakestIndex = j;
                        }
                    }
                }
            }

            return selected;
        }

        private void RenderTerrainBlock(int xi, int yi, bool after, int lodStep, TerrainBlock block = null)
        {
            if (!after) DrawnBlocks++;

            byte edgeMask = 0;
            if (!after && block != null)
                edgeMask = GetLodEdgeMask(block, lodStep);

            if (_lastBlendState != BlendState.Opaque)
            {
                _graphicsDevice.BlendState = BlendState.Opaque;
                _lastBlendState = BlendState.Opaque;
            }

            for (int i = 0; i < 4; i += lodStep)
            {
                for (int j = 0; j < 4; j += lodStep)
                {
                    // Use hierarchical culling if available
                    bool shouldRender = true;
                    if (block != null && !block.FullyVisible)
                    {
                        if (lodStep >= 4)
                        {
                            // Rendering the whole 4x4 block as one tile: render if any subtile is visible
                            shouldRender = block.VisibleTileCount > 0;
                        }
                        else if (lodStep == 2)
                        {
                            // Rendering a 2x2 super-tile: render if any of the 4 subtiles are visible
                            bool anyVisible = false;
                            for (int dy = 0; dy < 2 && !anyVisible; dy++)
                            {
                                for (int dx = 0; dx < 2; dx++)
                                {
                                    int ii = i + dy;
                                    int jj = j + dx;
                                    int idx = ii * 4 + jj;
                                    if (idx >= 0 && idx < block.TileVisibility.Length && block.TileVisibility[idx])
                                    {
                                        anyVisible = true;
                                        break;
                                    }
                                }
                            }
                            shouldRender = anyVisible;
                        }
                        else
                        {
                            // lodStep == 1: per-tile visibility
                            int tileIdx = i * 4 + j;
                            if (tileIdx < block.TileVisibility.Length)
                            {
                                shouldRender = block.TileVisibility[tileIdx];
                            }
                        }
                    }

                    if (shouldRender)
                    {
                        byte tileEdgeMask = 0;
                        if (edgeMask != 0)
                        {
                            if ((edgeMask & LodEdgeNorth) != 0 && i == 0) tileEdgeMask |= LodEdgeNorth;
                            if ((edgeMask & LodEdgeSouth) != 0 && (i + lodStep) >= BlockSize) tileEdgeMask |= LodEdgeSouth;
                            if ((edgeMask & LodEdgeWest) != 0 && j == 0) tileEdgeMask |= LodEdgeWest;
                            if ((edgeMask & LodEdgeEast) != 0 && (j + lodStep) >= BlockSize) tileEdgeMask |= LodEdgeEast;
                        }

                        RenderTerrainTile(
                            xi + j, yi + i,
                            (float)lodStep, lodStep,
                            after,
                            tileEdgeMask);
                    }
                }
            }
        }

        private void RenderTerrainTile(int xi, int yi, float lodFactor, int lodInt, bool after, byte edgeMask = 0)
        {
            if (after || _data.Attributes == null || _data.Attributes.TerrainWall == null) return; // Added null check for TerrainWall
            DrawnCells++;

            if (!HasAnyGroundInTile(xi, yi, lodInt))
                return;

            int i1 = GetTerrainIndex(xi, yi);

            int i2 = GetTerrainIndex(xi + lodInt, yi);
            int i3 = GetTerrainIndex(xi + lodInt, yi + lodInt);
            int i4 = GetTerrainIndex(xi, yi + lodInt);

            if (_useTerrainIndexBatching)
            {
                byte a1i = i1 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i1] : (byte)0;
                byte a2i = i2 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i2] : (byte)0;
                byte a3i = i3 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i3] : (byte)0;
                byte a4i = i4 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i4] : (byte)0;

                bool isOpaqueIndex = (a1i & a2i & a3i & a4i) == 255;
                bool hasAlphaIndex = (a1i | a2i | a3i | a4i) != 0;

                ushort u1 = (ushort)i1;
                ushort u2 = (ushort)i2;
                ushort u3 = (ushort)i3;
                ushort u4 = (ushort)i4;

                if (isOpaqueIndex)
                {
                    RenderTextureIndexed(_data.Mapping.Layer2[i1], u1, u2, u3, u4, alphaLayer: false);
                }
                else
                {
                    RenderTextureIndexed(_data.Mapping.Layer1[i1], u1, u2, u3, u4, alphaLayer: false);
                    if (hasAlphaIndex)
                    {
                        RenderTextureIndexed(_data.Mapping.Layer2[i1], u1, u2, u3, u4, alphaLayer: true);
                    }
                }

                // Grass is a fine-detail effect; skip for super-tiles (lodInt > 1) to avoid huge per-frame CPU cost.
                if (lodInt == 1 && Constants.DRAW_GRASS)
                    _grassRenderer.RenderGrassForTile(xi, yi, xi, yi, lodFactor, WorldIndex);

                return;
            }

            PrepareTileVertices(xi, yi, i1, i2, i3, i4, lodFactor);
            PrepareTileLights(i1, i2, i3, i4);
            bool renderSkirts = edgeMask != 0;
            if (renderSkirts)
            {
                _tempTerrainLightsBase[0] = _tempTerrainLights[0];
                _tempTerrainLightsBase[1] = _tempTerrainLights[1];
                _tempTerrainLightsBase[2] = _tempTerrainLights[2];
                _tempTerrainLightsBase[3] = _tempTerrainLights[3];
            }

            byte a1 = i1 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i1] : (byte)0;
            byte a2 = i2 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i2] : (byte)0;
            byte a3 = i3 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i3] : (byte)0;
            byte a4 = i4 < _data.Mapping.Alpha.Length ? _data.Mapping.Alpha[i4] : (byte)0;

            bool isOpaque = (a1 & a2 & a3 & a4) == 255;
            bool hasAlpha = (a1 | a2 | a3 | a4) != 0;
            int baseTextureIndex = isOpaque ? _data.Mapping.Layer2[i1] : _data.Mapping.Layer1[i1];
            int alphaTextureIndex = _data.Mapping.Layer2[i1];

            if (isOpaque)
            {
                RenderTexture(baseTextureIndex, xi, yi, lodFactor, useBatch: true, alphaLayer: false);
            }
            else
            {
                RenderTexture(baseTextureIndex, xi, yi, lodFactor, useBatch: true, alphaLayer: false);
                if (hasAlpha)
                {
                    ApplyAlphaToLights(a1, a2, a3, a4);
                    RenderTexture(alphaTextureIndex, xi, yi, lodFactor, useBatch: true, alphaLayer: true);
                }
            }

            if (renderSkirts)
            {
                _tempTerrainLights[0] = _tempTerrainLightsBase[0];
                _tempTerrainLights[1] = _tempTerrainLightsBase[1];
                _tempTerrainLights[2] = _tempTerrainLightsBase[2];
                _tempTerrainLights[3] = _tempTerrainLightsBase[3];

                RenderTileSkirts(xi, yi, lodFactor, edgeMask, baseTextureIndex, alphaLayer: false);
                if (!isOpaque && hasAlpha)
                {
                    ApplyAlphaToLights(a1, a2, a3, a4);
                    RenderTileSkirts(xi, yi, lodFactor, edgeMask, alphaTextureIndex, alphaLayer: true);
                }
            }

            // Grass is a fine-detail effect; skip for super-tiles (lodInt > 1) to avoid huge per-frame CPU cost.
            if (lodInt == 1 && Constants.DRAW_GRASS)
                _grassRenderer.RenderGrassForTile(xi, yi, xi, yi, lodFactor, WorldIndex);
        }

        private void RenderTileSkirts(int xi, int yi, float lodFactor, byte edgeMask, int textureIndex, bool alphaLayer)
        {
            if (edgeMask == 0)
                return;
            if (textureIndex < 0 || textureIndex >= _data.Textures.Length || _data.Textures[textureIndex] == null)
                return;

            var uvScale = _tileUvScale[textureIndex];
            float baseW = uvScale.X;
            float baseH = uvScale.Y;
            float suf = xi * baseW;
            float svf = yi * baseH;
            float uvW = baseW * lodFactor;
            float uvH = baseH * lodFactor;

            Vector2 uv0 = new Vector2(suf, svf);
            Vector2 uv1 = new Vector2(suf + uvW, svf);
            Vector2 uv2 = new Vector2(suf + uvW, svf + uvH);
            Vector2 uv3 = new Vector2(suf, svf + uvH);

            Vector3 v0 = _tempTerrainVertex[0];
            Vector3 v1 = _tempTerrainVertex[1];
            Vector3 v2 = _tempTerrainVertex[2];
            Vector3 v3 = _tempTerrainVertex[3];

            Vector3 v0b = v0; v0b.Z -= LodSkirtDepth;
            Vector3 v1b = v1; v1b.Z -= LodSkirtDepth;
            Vector3 v2b = v2; v2b.Z -= LodSkirtDepth;
            Vector3 v3b = v3; v3b.Z -= LodSkirtDepth;

            Color c0 = _tempTerrainLights[0];
            Color c1 = _tempTerrainLights[1];
            Color c2 = _tempTerrainLights[2];
            Color c3 = _tempTerrainLights[3];

            Vector3 n0 = _tempTerrainNormals[0];
            Vector3 n1 = _tempTerrainNormals[1];
            Vector3 n2 = _tempTerrainNormals[2];
            Vector3 n3 = _tempTerrainNormals[3];

            if ((edgeMask & LodEdgeNorth) != 0)
                AddSkirtQuad(textureIndex, v0, v1, v0b, v1b, c0, c1, n0, n1, uv0, uv1, alphaLayer);
            if ((edgeMask & LodEdgeSouth) != 0)
                AddSkirtQuad(textureIndex, v3, v2, v3b, v2b, c3, c2, n3, n2, uv3, uv2, alphaLayer);
            if ((edgeMask & LodEdgeWest) != 0)
                AddSkirtQuad(textureIndex, v0, v3, v0b, v3b, c0, c3, n0, n3, uv0, uv3, alphaLayer);
            if ((edgeMask & LodEdgeEast) != 0)
                AddSkirtQuad(textureIndex, v1, v2, v1b, v2b, c1, c2, n1, n2, uv1, uv2, alphaLayer);
        }

        private void AddSkirtQuad(
            int textureIndex,
            Vector3 topA,
            Vector3 topB,
            Vector3 bottomA,
            Vector3 bottomB,
            Color colorA,
            Color colorB,
            Vector3 normalA,
            Vector3 normalB,
            Vector2 uvA,
            Vector2 uvB,
            bool alphaLayer)
        {
            _terrainVertices[0] = new TerrainVertexPositionColorNormalTexture(topA, colorA, normalA, uvA);
            _terrainVertices[1] = new TerrainVertexPositionColorNormalTexture(topB, colorB, normalB, uvB);
            _terrainVertices[2] = new TerrainVertexPositionColorNormalTexture(bottomB, colorB, normalB, uvB);
            _terrainVertices[3] = new TerrainVertexPositionColorNormalTexture(bottomB, colorB, normalB, uvB);
            _terrainVertices[4] = new TerrainVertexPositionColorNormalTexture(bottomA, colorA, normalA, uvA);
            _terrainVertices[5] = new TerrainVertexPositionColorNormalTexture(topA, colorA, normalA, uvA);

            AddTileToBatch(textureIndex, _terrainVertices, alphaLayer);

            _terrainVertices[0] = new TerrainVertexPositionColorNormalTexture(topA, colorA, normalA, uvA);
            _terrainVertices[1] = new TerrainVertexPositionColorNormalTexture(bottomA, colorA, normalA, uvA);
            _terrainVertices[2] = new TerrainVertexPositionColorNormalTexture(bottomB, colorB, normalB, uvB);
            _terrainVertices[3] = new TerrainVertexPositionColorNormalTexture(bottomB, colorB, normalB, uvB);
            _terrainVertices[4] = new TerrainVertexPositionColorNormalTexture(topB, colorB, normalB, uvB);
            _terrainVertices[5] = new TerrainVertexPositionColorNormalTexture(topA, colorA, normalA, uvA);

            AddTileToBatch(textureIndex, _terrainVertices, alphaLayer);
        }

        private bool HasAnyGroundInTile(int xi, int yi, int lodInt)
        {
            var terrainWall = _data.Attributes?.TerrainWall;
            if (terrainWall == null)
                return true;

            if (lodInt <= 1)
            {
                int idx = GetTerrainIndex(xi, yi);
                return (uint)idx < (uint)terrainWall.Length &&
                       !terrainWall[idx].HasFlag(Client.Data.ATT.TWFlags.NoGround);
            }

            int max = Constants.TERRAIN_SIZE - 1;
            int endX = Math.Min(xi + lodInt - 1, max);
            int endY = Math.Min(yi + lodInt - 1, max);

            for (int y = yi; y <= endY; y++)
            {
                int row = y * Constants.TERRAIN_SIZE;
                for (int x = xi; x <= endX; x++)
                {
                    int idx = row + x;
                    if ((uint)idx < (uint)terrainWall.Length &&
                        !terrainWall[idx].HasFlag(Client.Data.ATT.TWFlags.NoGround))
                        return true;
                }
            }

            return false;
        }

        private void RenderTexture(int textureIndex, float xf, float yf, float lodScale, bool useBatch, bool alphaLayer = false)
        {
            if (textureIndex < 0 || textureIndex >= _data.Textures.Length || _data.Textures[textureIndex] == null)
                return;

            var texture = _data.Textures[textureIndex];
            var uvScale = _tileUvScale[textureIndex];
            float baseW = uvScale.X;
            float baseH = uvScale.Y;
            float suf = xf * baseW;
            float svf = yf * baseH;
            float uvW = baseW * lodScale;
            float uvH = baseH * lodScale;

            bool noFlow = WaterSpeed == 0f && DistortionAmplitude == 0f;
            if (textureIndex == 5 && !noFlow) // Water with flow/distortion
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

            _terrainVertices[0] = new TerrainVertexPositionColorNormalTexture(_tempTerrainVertex[0], _tempTerrainLights[0], _tempTerrainNormals[0], _terrainTextureCoords[0]);
            _terrainVertices[1] = new TerrainVertexPositionColorNormalTexture(_tempTerrainVertex[1], _tempTerrainLights[1], _tempTerrainNormals[1], _terrainTextureCoords[1]);
            _terrainVertices[2] = new TerrainVertexPositionColorNormalTexture(_tempTerrainVertex[2], _tempTerrainLights[2], _tempTerrainNormals[2], _terrainTextureCoords[2]);
            _terrainVertices[3] = new TerrainVertexPositionColorNormalTexture(_tempTerrainVertex[2], _tempTerrainLights[2], _tempTerrainNormals[2], _terrainTextureCoords[2]);
            _terrainVertices[4] = new TerrainVertexPositionColorNormalTexture(_tempTerrainVertex[3], _tempTerrainLights[3], _tempTerrainNormals[3], _terrainTextureCoords[3]);
            _terrainVertices[5] = new TerrainVertexPositionColorNormalTexture(_tempTerrainVertex[0], _tempTerrainLights[0], _tempTerrainNormals[0], _terrainTextureCoords[0]);

            if (useBatch)
            {
                AddTileToBatch(textureIndex, _terrainVertices, alphaLayer);
            }
            else
            {
                if (_useDynamicLightingShader)
                {
                    var effect = GraphicsManager.Instance.DynamicLightingEffect;
                    if (effect == null) return;

                    effect.Parameters["DiffuseTexture"]?.SetValue(texture);
                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, 2);
                    }
                }
                else
                {
                    var basicEffect = GraphicsManager.Instance.BasicEffect3D;
                    if (basicEffect == null) return;

                    basicEffect.Texture = texture;
                    for (int i = 0; i < 6; i++)
                    {
                        var v = _terrainVertices[i];
                        _fallbackTileBuffer[i] = new VertexPositionColorTexture(v.Position, v.Color, v.TextureCoordinate);
                    }
                    foreach (var pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _fallbackTileBuffer, 0, 2);
                    }
                }
                DrawCalls++;
                DrawnTriangles += 2;
            }
        }

        private void PrepareTileVertices(int xi, int yi, int i1, int i2, int i3, int i4, float lodFactor)
        {
            if (_cachedVertexPositions != null && _cachedVertexNormals != null)
            {
                int total = _cachedVertexPositions.Length;
                if ((uint)i1 < (uint)total &&
                    (uint)i2 < (uint)total &&
                    (uint)i3 < (uint)total &&
                    (uint)i4 < (uint)total)
                {
                    // Cached positions include height scaling and special-height offsets.
                    _tempTerrainVertex[0] = _cachedVertexPositions[i1];
                    _tempTerrainVertex[1] = _cachedVertexPositions[i2];
                    _tempTerrainVertex[2] = _cachedVertexPositions[i3];
                    _tempTerrainVertex[3] = _cachedVertexPositions[i4];

                    _tempTerrainNormals[0] = _cachedVertexNormals[i1];
                    _tempTerrainNormals[1] = _cachedVertexNormals[i2];
                    _tempTerrainNormals[2] = _cachedVertexNormals[i3];
                    _tempTerrainNormals[3] = _cachedVertexNormals[i4];
                    return;
                }
            }

            // Fallback for edge tiles that exceed cached array bounds (matches pre-cache behavior).
            float sx = xi * Constants.TERRAIN_SCALE;
            float sy = yi * Constants.TERRAIN_SCALE;
            float ss = Constants.TERRAIN_SCALE * lodFactor;

            SetTempVertex(0, i1, sx, sy);
            SetTempVertex(1, i2, sx + ss, sy);
            SetTempVertex(2, i3, sx + ss, sy + ss);
            SetTempVertex(3, i4, sx, sy + ss);
        }

        private void SetTempVertex(int slot, int index, float x, float y)
        {
            if (_cachedVertexPositions != null &&
                _cachedVertexNormals != null &&
                (uint)index < (uint)_cachedVertexPositions.Length &&
                (uint)index < (uint)_cachedVertexNormals.Length)
            {
                _tempTerrainVertex[slot] = _cachedVertexPositions[index];
                _tempTerrainNormals[slot] = _cachedVertexNormals[index];
                return;
            }

            float z = 0f;
            if (_data.HeightMap != null && (uint)index < (uint)_data.HeightMap.Length)
            {
                z = _data.HeightMap[index].R * 1.5f;
                var terrainWall = _data.Attributes?.TerrainWall;
                if (terrainWall != null &&
                    (uint)index < (uint)terrainWall.Length &&
                    (terrainWall[index] & Client.Data.ATT.TWFlags.Height) != 0)
                {
                    z += SpecialHeight;
                }
            }

            _tempTerrainVertex[slot] = new Vector3(x, y, z);
            _tempTerrainNormals[slot] = Vector3.UnitZ;
        }

        private void PrepareTileLights(int i1, int i2, int i3, int i4)
        {
            _tempTerrainLights[0] = GetVertexBaseLight(i1);
            _tempTerrainLights[1] = GetVertexBaseLight(i2);
            _tempTerrainLights[2] = GetVertexBaseLight(i3);
            _tempTerrainLights[3] = GetVertexBaseLight(i4);

            // CPU fallback path: bake dynamic lights into vertex color (shader path does dynamic lighting on GPU).
            if (!_useDynamicLightingShader && _data.FinalLightMap != null)
            {
                ApplyCpuDynamicLight(ref _tempTerrainLights[0], _tempTerrainVertex[0]);
                ApplyCpuDynamicLight(ref _tempTerrainLights[1], _tempTerrainVertex[1]);
                ApplyCpuDynamicLight(ref _tempTerrainLights[2], _tempTerrainVertex[2]);
                ApplyCpuDynamicLight(ref _tempTerrainLights[3], _tempTerrainVertex[3]);
            }
        }

        private Color GetVertexBaseLight(int index)
        {
            if (_cachedVertexBaseLights != null && (uint)index < (uint)_cachedVertexBaseLights.Length)
                return _cachedVertexBaseLights[index];

            var finalLightMap = _data.FinalLightMap;
            if (finalLightMap == null)
                return Color.White;

            Vector3 baseColor = Vector3.Zero;
            if ((uint)index < (uint)finalLightMap.Length)
            {
                var c = finalLightMap[index];
                baseColor = new Vector3(c.R, c.G, c.B);
            }

            baseColor += new Vector3(AmbientLight * 255f);
            baseColor = Vector3.Clamp(baseColor, Vector3.Zero, new Vector3(255f));
            return new Color((int)baseColor.X, (int)baseColor.Y, (int)baseColor.Z);
        }

        private void ApplyCpuDynamicLight(ref Color baseLight, Vector3 pos)
        {
            var dyn = _lightManager.EvaluateDynamicLight(new Vector2(pos.X, pos.Y));
            if (dyn.LengthSquared() < 0.0001f)
                return;

            float r = MathF.Min(baseLight.R + dyn.X, 255f);
            float g = MathF.Min(baseLight.G + dyn.Y, 255f);
            float b = MathF.Min(baseLight.B + dyn.Z, 255f);

            baseLight = new Color((byte)r, (byte)g, (byte)b, baseLight.A);
        }

        private void ApplyAlphaToLights(byte a1, byte a2, byte a3, byte a4)
        {
            _tempTerrainLights[0] *= a1 / 255f; _tempTerrainLights[0].A = a1;
            _tempTerrainLights[1] *= a2 / 255f; _tempTerrainLights[1].A = a2;
            _tempTerrainLights[2] *= a3 / 255f; _tempTerrainLights[2].A = a3;
            _tempTerrainLights[3] *= a4 / 255f; _tempTerrainLights[3].A = a4;
        }

        private static bool SupportsProceduralTerrainUv(Effect effect)
        {
            if (effect?.Parameters == null)
                return false;

            return effect.Parameters["UseProceduralTerrainUV"] != null &&
                   effect.Parameters["TerrainUvScale"] != null &&
                   effect.Parameters["IsWaterTexture"] != null;
        }

        private void EnsureTerrainVertexBuffers()
        {
            if (_graphicsDevice == null)
                return;

            EnsureVertexCache();
            EnsureLightCache();

            int total = Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE;
            if (_cachedVertexPositions == null || _cachedVertexNormals == null || _cachedVertexBaseLights == null)
                return;
            if (_cachedVertexPositions.Length < total || _cachedVertexNormals.Length < total || _cachedVertexBaseLights.Length < total)
                return;

            var alphaMap = _data.Mapping.Alpha;
            if (alphaMap != null && alphaMap.Length < total)
                alphaMap = null;

            bool needsRebuild =
                !_terrainVertexBuffersBuilt ||
                _terrainVertexBufferBase == null ||
                _terrainVertexBufferBase.IsDisposed ||
                _terrainVertexBufferAlpha == null ||
                _terrainVertexBufferAlpha.IsDisposed ||
                !ReferenceEquals(_terrainBuffersHeightMapRef, _cachedHeightMapRef) ||
                !ReferenceEquals(_terrainBuffersTerrainWallRef, _cachedTerrainWallRef) ||
                !ReferenceEquals(_terrainBuffersNormalsRef, _cachedNormalsRef) ||
                !ReferenceEquals(_terrainBuffersFinalLightMapRef, _cachedFinalLightMapRef) ||
                Math.Abs(_terrainBuffersAmbientLight - _cachedAmbientLight) > 0.0001f ||
                !ReferenceEquals(_terrainBuffersAlphaMapRef, alphaMap);

            if (!needsRebuild)
                return;

            _terrainVertexBufferBase?.Dispose();
            _terrainVertexBufferAlpha?.Dispose();

            _terrainVertexBufferBase = new VertexBuffer(
                _graphicsDevice,
                TerrainVertexPositionColorNormalTexture.VertexDeclaration,
                total,
                BufferUsage.WriteOnly);

            _terrainVertexBufferAlpha = new VertexBuffer(
                _graphicsDevice,
                TerrainVertexPositionColorNormalTexture.VertexDeclaration,
                total,
                BufferUsage.WriteOnly);

            var verts = ArrayPool<TerrainVertexPositionColorNormalTexture>.Shared.Rent(total);
            try
            {
                // Base buffer: baked static light (A=255), procedural UV in shader (TexCoord unused).
                for (int i = 0; i < total; i++)
                {
                    verts[i] = new TerrainVertexPositionColorNormalTexture(
                        _cachedVertexPositions[i],
                        _cachedVertexBaseLights[i],
                        _cachedVertexNormals[i],
                        Vector2.Zero);
                }
                _terrainVertexBufferBase.SetData(verts, 0, total);

                // Alpha buffer: premultiply baked light by alpha map (matches old CPU path).
                for (int i = 0; i < total; i++)
                {
                    Color baseLight = _cachedVertexBaseLights[i];
                    byte a = alphaMap != null ? alphaMap[i] : (byte)0;

                    int r = baseLight.R * a / 255;
                    int g = baseLight.G * a / 255;
                    int b = baseLight.B * a / 255;

                    verts[i] = new TerrainVertexPositionColorNormalTexture(
                        _cachedVertexPositions[i],
                        new Color((byte)r, (byte)g, (byte)b, a),
                        _cachedVertexNormals[i],
                        Vector2.Zero);
                }
                _terrainVertexBufferAlpha.SetData(verts, 0, total);
            }
            finally
            {
                ArrayPool<TerrainVertexPositionColorNormalTexture>.Shared.Return(verts);
            }

            _terrainBuffersHeightMapRef = _cachedHeightMapRef;
            _terrainBuffersTerrainWallRef = _cachedTerrainWallRef;
            _terrainBuffersNormalsRef = _cachedNormalsRef;
            _terrainBuffersFinalLightMapRef = _cachedFinalLightMapRef;
            _terrainBuffersAmbientLight = _cachedAmbientLight;
            _terrainBuffersAlphaMapRef = alphaMap;
            _terrainVertexBuffersBuilt = true;
        }

        private void RenderTextureIndexed(int textureIndex, ushort i1, ushort i2, ushort i3, ushort i4, bool alphaLayer)
        {
            if (textureIndex < 0 || textureIndex >= _data.Textures.Length || _data.Textures[textureIndex] == null)
                return;

            AddTileToIndexBatch(textureIndex, i1, i2, i3, i4, alphaLayer);
        }

        private void AddTileToIndexBatch(int texIndex, ushort i1, ushort i2, ushort i3, ushort i4, bool alphaLayer)
        {
            var batch = GetTileIndexBatchBuffer(texIndex, alphaLayer);
            var counters = alphaLayer ? _tileAlphaIndexCounts : _tileIndexCounts;

            int dstOff = counters[texIndex];
            if (dstOff + 6 > TileBatchIndices)
            {
                FlushSingleTextureIndexed(texIndex, alphaLayer);
                dstOff = 0;
            }

            batch[dstOff + 0] = i1;
            batch[dstOff + 1] = i2;
            batch[dstOff + 2] = i3;
            batch[dstOff + 3] = i3;
            batch[dstOff + 4] = i4;
            batch[dstOff + 5] = i1;
            counters[texIndex] = dstOff + 6;
        }

        private void FlushSingleTextureIndexed(int texIndex, bool alphaLayer)
        {
            int indexCount = alphaLayer ? _tileAlphaIndexCounts[texIndex] : _tileIndexCounts[texIndex];
            if (indexCount == 0)
                return;

            var batch = GetTileIndexBatchBuffer(texIndex, alphaLayer);
            var texture = _data.Textures[texIndex];
            if (texture == null)
            {
                if (alphaLayer) _tileAlphaIndexCounts[texIndex] = 0;
                else _tileIndexCounts[texIndex] = 0;
                return;
            }

            var effect = GraphicsManager.Instance.DynamicLightingEffect;
            if (effect == null || effect.CurrentTechnique == null)
                return;

            var blendState = alphaLayer ? BlendState.AlphaBlend : BlendState.Opaque;
            if (_lastBlendState != blendState)
            {
                _graphicsDevice.BlendState = blendState;
                _lastBlendState = blendState;
            }

            // Texture + per-texture UV scale and water toggle
            if (_lastBoundTexture != texture)
            {
                effect.Parameters["DiffuseTexture"]?.SetValue(texture);
                _lastBoundTexture = texture;
            }

            effect.Parameters["TerrainUvScale"]?.SetValue(_tileUvScaleWorld[texIndex]);

            bool noFlow = WaterSpeed == 0f && DistortionAmplitude == 0f;
            float isWater = (texIndex == 5 && !noFlow) ? 1.0f : 0.0f;
            effect.Parameters["IsWaterTexture"]?.SetValue(isWater);

            var vb = alphaLayer ? _terrainVertexBufferAlpha : _terrainVertexBufferBase;
            if (vb == null || vb.IsDisposed)
                return;

            var indexBuffer = DynamicBufferPool.RentIndexBuffer(indexCount, prefer16Bit: true);
            if (indexBuffer == null)
            {
                if (alphaLayer) _tileAlphaIndexCounts[texIndex] = 0;
                else _tileIndexCounts[texIndex] = 0;
                return;
            }

            try
            {
                indexBuffer.SetData(batch, 0, indexCount, SetDataOptions.Discard);
                _graphicsDevice.SetVertexBuffer(vb);
                _graphicsDevice.Indices = indexBuffer;

                int primitiveCount = indexCount / 3;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }

                DrawCalls++;
                DrawnTriangles += primitiveCount;
            }
            finally
            {
                _graphicsDevice.SetVertexBuffer(null);
                _graphicsDevice.Indices = null;
                DynamicBufferPool.ReturnIndexBuffer(indexBuffer);
            }

            if (alphaLayer) _tileAlphaIndexCounts[texIndex] = 0;
            else _tileIndexCounts[texIndex] = 0;
        }

        private void FlushAllTileIndexBatches()
        {
            // First pass: render all opaque batches
            for (int t = 0; t < 256; t++)
            {
                if (_tileIndexCounts[t] > 0)
                    FlushSingleTextureIndexed(t, alphaLayer: false);
            }

            // Second pass: render all alpha batches
            for (int t = 0; t < 256; t++)
            {
                if (_tileAlphaIndexCounts[t] > 0)
                    FlushSingleTextureIndexed(t, alphaLayer: true);
            }

            if (_lastBlendState != BlendState.Opaque)
            {
                _graphicsDevice.BlendState = BlendState.Opaque;
                _lastBlendState = BlendState.Opaque;
            }
        }

        private void AddTileToBatch(int texIndex, TerrainVertexPositionColorNormalTexture[] verts, bool alphaLayer)
        {
            var batch = GetTileBatchBuffer(texIndex, alphaLayer);
            var counters = alphaLayer ? _tileAlphaCounts : _tileBatchCounts;

            int dstOff = counters[texIndex];
            if (dstOff + 6 > TileBatchVerts)
            {
                FlushSingleTexture(texIndex, alphaLayer);
                dstOff = 0;
            }

            // Manual unroll for better performance than Array.Copy
            batch[dstOff + 0] = verts[0];
            batch[dstOff + 1] = verts[1];
            batch[dstOff + 2] = verts[2];
            batch[dstOff + 3] = verts[3];
            batch[dstOff + 4] = verts[4];
            batch[dstOff + 5] = verts[5];
            counters[texIndex] = dstOff + 6;
        }

        private void FlushSingleTexture(int texIndex, bool alphaLayer)
        {
            int vertCount = alphaLayer ? _tileAlphaCounts[texIndex] : _tileBatchCounts[texIndex];
            if (vertCount == 0) return;

            var batch = GetTileBatchBuffer(texIndex, alphaLayer);
            var texture = _data.Textures[texIndex];
            if (texture == null) return; // Added null check for texture
            var blendState = alphaLayer ? BlendState.AlphaBlend : BlendState.Opaque;

            if (_useDynamicLightingShader)
            {
                var effect = GraphicsManager.Instance.DynamicLightingEffect;
                if (effect == null || effect.CurrentTechnique == null) return; // Added null checks for effect and effect.CurrentTechnique

                int triCount = vertCount / 3;
                // Avoid unnecessary state changes
                if (_lastBoundTexture != texture)
                {
                    effect.Parameters["DiffuseTexture"]?.SetValue(texture);
                    _lastBoundTexture = texture;
                }

                if (_lastBlendState != blendState)
                {
                    _graphicsDevice.BlendState = blendState;
                    _lastBlendState = blendState;
                }

                var vertexBuffer = DynamicBufferPool.RentVertexBuffer(vertCount);
                if (vertexBuffer == null)
                {
                    // Fallback to old path if pooling unavailable
                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _graphicsDevice.DrawUserPrimitives(
                            PrimitiveType.TriangleList,
                            batch, 0,
                            triCount);
                    }
                }
                else
                {
                    try
                    {
                        vertexBuffer.SetData(batch, 0, vertCount, SetDataOptions.Discard);
                        _graphicsDevice.SetVertexBuffer(vertexBuffer);

                        foreach (var pass in effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            _graphicsDevice.DrawPrimitives(
                                PrimitiveType.TriangleList,
                                0,
                                triCount);
                        }
                    }
                    finally
                    {
                        _graphicsDevice.SetVertexBuffer(null);
                        DynamicBufferPool.ReturnVertexBuffer(vertexBuffer);
                    }
                }
            }
            else
            {
                var effect = GraphicsManager.Instance.BasicEffect3D;
                if (effect == null || effect.CurrentTechnique == null) return; // Added null checks for effect and effect.CurrentTechnique

                // Avoid unnecessary state changes
                if (_lastBoundTexture != texture)
                {
                    effect.Texture = texture;
                    _lastBoundTexture = texture;
                }

                if (_lastBlendState != blendState)
                {
                    _graphicsDevice.BlendState = blendState;
                    _lastBlendState = blendState;
                }

                for (int i = 0; i < vertCount; i++)
                {
                    var v = batch[i];
                    _fallbackTileBuffer[i] = new VertexPositionColorTexture(v.Position, v.Color, v.TextureCoordinate);
                }

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        _fallbackTileBuffer, 0,
                        vertCount / 3);
                }
            }

            DrawCalls++;
            DrawnTriangles += vertCount / 3;

            if (alphaLayer) _tileAlphaCounts[texIndex] = 0;
            else _tileBatchCounts[texIndex] = 0;
        }

        private void FlushAllTileBatches()
        {
            // IMPORTANT: For proper terrain blending, we must render ALL opaque layers first,
            // then ALL alpha layers. This preserves the correct depth/blend order.

            // First pass: render all opaque batches
            for (int t = 0; t < 256; t++)
            {
                if (_tileBatchCounts[t] > 0)
                    FlushSingleTexture(t, alphaLayer: false);
            }

            // Second pass: render all alpha batches
            for (int t = 0; t < 256; t++)
            {
                if (_tileAlphaCounts[t] > 0)
                    FlushSingleTexture(t, alphaLayer: true);
            }

            if (_lastBlendState != BlendState.Opaque)
            {
                _graphicsDevice.BlendState = BlendState.Opaque;
                _lastBlendState = BlendState.Opaque;
            }
        }

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;

        private TerrainVertexPositionColorNormalTexture[] GetTileBatchBuffer(int texIndex, bool alphaLayer)
        {
            var batches = alphaLayer ? _tileAlphaBatches : _tileBatches;
            var buffer = batches[texIndex];
            if (buffer == null || buffer.Length != TileBatchVerts)
            {
                buffer = new TerrainVertexPositionColorNormalTexture[TileBatchVerts];
                batches[texIndex] = buffer;
            }
            return buffer;
        }

        private ushort[] GetTileIndexBatchBuffer(int texIndex, bool alphaLayer)
        {
            var batches = alphaLayer ? _tileAlphaIndexBatches : _tileIndexBatches;
            var buffer = batches[texIndex];
            if (buffer == null || buffer.Length != TileBatchIndices)
            {
                buffer = new ushort[TileBatchIndices];
                batches[texIndex] = buffer;
            }
            return buffer;
        }
    }
}
