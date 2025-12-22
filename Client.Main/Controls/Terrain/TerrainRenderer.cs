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
        private const int TileBatchVerts = 16384 * 6;

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

        // Buffers for a single terrain tile quad
        private readonly TerrainVertexPositionColorNormalTexture[] _terrainVertices = new TerrainVertexPositionColorNormalTexture[6];
        private readonly Vector2[] _terrainTextureCoords = new Vector2[4];
        private readonly Vector3[] _tempTerrainVertex = new Vector3[4];
        private readonly Vector3[] _tempTerrainNormals = new Vector3[4];
        private readonly Color[] _tempTerrainLights = new Color[4];
        private readonly VertexPositionColorTexture[] _fallbackTileBuffer = new VertexPositionColorTexture[TileBatchVerts];

        // State tracking for GPU optimization
        private Texture2D _lastBoundTexture = null;
        private BlendState _lastBlendState = null;
        private bool _useDynamicLightingShader = false;

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

            // Precompute UV scales for all textures
            PrecomputeUVScales();
        }

        private void PrecomputeUVScales()
        {
            for (int t = 0; t < _data.Textures.Length; t++)
            {
                if (_data.Textures[t] != null)
                {
                    _tileUvScale[t] = new Vector2(64f / _data.Textures[t].Width, 64f / _data.Textures[t].Height);
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

            if (_useDynamicLightingShader)
            {
                var effect = GraphicsManager.Instance.DynamicLightingEffect;
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

        public void DrawShadowMap(Effect shadowEffect, Matrix lightViewProjection)
        {
            if (_graphicsDevice == null || _data.HeightMap == null || shadowEffect == null)
                return;

            var prevBlend = _graphicsDevice.BlendState;
            var prevDepth = _graphicsDevice.DepthStencilState;
            var prevRaster = _graphicsDevice.RasterizerState;
            var prevTechnique = shadowEffect.CurrentTechnique;
            var prevUseDynamic = _useDynamicLightingShader;
            var prevLastBoundTexture = _lastBoundTexture;

            // Reset texture binding to ensure correct textures are bound for shadow pass
            _lastBoundTexture = null;
            _useDynamicLightingShader = true;
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

            FlushAllTileBatches();

            _useDynamicLightingShader = prevUseDynamic;
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
                        RenderTerrainTile(
                            xi + j, yi + i,
                            (float)lodStep, lodStep,
                            after);
                    }
                }
            }
        }

        private void RenderTerrainTile(int xi, int yi, float lodFactor, int lodInt, bool after)
        {
            if (after || _data.Attributes == null || _data.Attributes.TerrainWall == null) return; // Added null check for TerrainWall
            DrawnCells++;

            int i1 = GetTerrainIndex(xi, yi);
            if (_data.Attributes.TerrainWall[i1].HasFlag(Client.Data.ATT.TWFlags.NoGround)) return;

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
            float h1 = i1 < _data.HeightMap.Length ? _data.HeightMap[i1].R * 1.5f : 0f;
            float h2 = i2 < _data.HeightMap.Length ? _data.HeightMap[i2].R * 1.5f : 0f;
            float h3 = i3 < _data.HeightMap.Length ? _data.HeightMap[i3].R * 1.5f : 0f;
            float h4 = i4 < _data.HeightMap.Length ? _data.HeightMap[i4].R * 1.5f : 0f;

            float sx = xi * Constants.TERRAIN_SCALE;
            float sy = yi * Constants.TERRAIN_SCALE;
            float ss = Constants.TERRAIN_SCALE * lodFactor;

            _tempTerrainVertex[0] = new Vector3(sx, sy, h1);
            _tempTerrainVertex[1] = new Vector3(sx + ss, sy, h2);
            _tempTerrainVertex[2] = new Vector3(sx + ss, sy + ss, h3);
            _tempTerrainVertex[3] = new Vector3(sx, sy + ss, h4);

            if (i1 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i1].HasFlag(Client.Data.ATT.TWFlags.Height))
                _tempTerrainVertex[0].Z += SpecialHeight;
            if (i2 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i2].HasFlag(Client.Data.ATT.TWFlags.Height))
                _tempTerrainVertex[1].Z += SpecialHeight;
            if (i3 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i3].HasFlag(Client.Data.ATT.TWFlags.Height))
                _tempTerrainVertex[2].Z += SpecialHeight;
            if (i4 < _data.Attributes.TerrainWall.Length && _data.Attributes.TerrainWall[i4].HasFlag(Client.Data.ATT.TWFlags.Height))
                _tempTerrainVertex[3].Z += SpecialHeight;

            _tempTerrainNormals[0] = GetTerrainNormal(i1);
            _tempTerrainNormals[1] = GetTerrainNormal(i2);
            _tempTerrainNormals[2] = GetTerrainNormal(i3);
            _tempTerrainNormals[3] = GetTerrainNormal(i4);
        }

        private void PrepareTileLights(int i1, int i2, int i3, int i4)
        {
            _tempTerrainLights[0] = BuildVertexLight(i1, _tempTerrainVertex[0]);
            _tempTerrainLights[1] = BuildVertexLight(i2, _tempTerrainVertex[1]);
            _tempTerrainLights[2] = BuildVertexLight(i3, _tempTerrainVertex[2]);
            _tempTerrainLights[3] = BuildVertexLight(i4, _tempTerrainVertex[3]);
        }

        private Vector3 GetTerrainNormal(int index)
        {
            if (_data.Normals == null || (uint)index >= (uint)_data.Normals.Length)
                return Vector3.UnitZ;

            var normal = _data.Normals[index];
            if (normal.LengthSquared() < 1e-6f)
                return Vector3.UnitZ;

            return normal;
        }

        private Color BuildVertexLight(int index, Vector3 pos)
        {
            if (_data.FinalLightMap == null) return Color.White; // Added null check for _data.FinalLightMap

            // Calculate lighting (expensive operation)
            Vector3 baseColor = index < _data.FinalLightMap.Length
                ? new Vector3(_data.FinalLightMap[index].R, _data.FinalLightMap[index].G, _data.FinalLightMap[index].B)
                : Vector3.Zero;

            baseColor += new Vector3(AmbientLight * 255f);
            if (!_useDynamicLightingShader)
            {
                baseColor += _lightManager.EvaluateDynamicLight(new Vector2(pos.X, pos.Y));
            }
            baseColor = Vector3.Clamp(baseColor, Vector3.Zero, new Vector3(255f));

            Color result = new Color((int)baseColor.X, (int)baseColor.Y, (int)baseColor.Z);

            return result;
        }

        private void ApplyAlphaToLights(byte a1, byte a2, byte a3, byte a4)
        {
            _tempTerrainLights[0] *= a1 / 255f; _tempTerrainLights[0].A = a1;
            _tempTerrainLights[1] *= a2 / 255f; _tempTerrainLights[1].A = a2;
            _tempTerrainLights[2] *= a3 / 255f; _tempTerrainLights[2].A = a3;
            _tempTerrainLights[3] *= a4 / 255f; _tempTerrainLights[3].A = a4;
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
    }
}
