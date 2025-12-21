using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using static Client.Main.Core.Utilities.Utils;
using Client.Main.Content;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Renders grass tufts on the terrain.
    /// </summary>
    public class GrassRenderer
    {
        // Grass density distances (squared)
        private const float GrassNearSq = 3000f * 3000f;   // full density
        private const float GrassMidSq = 4000f * 4000f;   // two tufts
        private const float GrassFarSq = 5000f * 5000f;   // one tuft
        private const int GrassBatchQuads = 16384;
        private const int GrassBatchVerts = GrassBatchQuads * 6;

        // Optimization constants
        private static readonly float Cos45 = 0.70710678f;
        private static readonly float Sin45 = 0.70710678f;
        private volatile bool _texReady;

        // Optimization: Direct pseudo-random calculation is faster than dictionary lookups

        private readonly GraphicsDevice _graphicsDevice;
        private readonly TerrainData _data;
        private readonly TerrainPhysics _physics;
        private readonly TerrainLightManager _lightManager;
        private readonly WindSimulator _wind;

        private Texture2D _grassSpriteTexture;
        private AlphaTestEffect _grassEffect;
        private string _grassSpritePath;

        private readonly VertexPositionColorTexture[] _grassBatch = new VertexPositionColorTexture[GrassBatchVerts];
        private int _grassBatchCount = 0;

        public float GrassBrightness { get; set; } = 2f;
        public HashSet<byte> GrassTextureIndices { get; } = new() { 0 };
        public int Flushes { get; private set; }
        public int DrawnTriangles { get; private set; }

        public GrassRenderer(GraphicsDevice graphicsDevice, TerrainData data, TerrainPhysics physics, WindSimulator wind, TerrainLightManager lightManager)
        {
            _graphicsDevice = graphicsDevice;
            _data = data;
            _physics = physics;
            _wind = wind;
            _lightManager = lightManager;
        }

        // Track which textures have already been premultiplied to avoid repeated darkening across warps
        private static readonly object _premulLock = new();
        private static readonly HashSet<string> _premultipliedOnce = new(StringComparer.OrdinalIgnoreCase);

        public async void LoadContent(short worldIndex)
        {
            if (Constants.DRAW_GRASS)
            {
                string textureFile = worldIndex switch
                {
                    3 => "TileGrass02.ozt", // Devias
                    _ => "TileGrass01.ozt"
                };

                _grassSpritePath = Path.Combine($"World{worldIndex}", textureFile);
                try
                {
                    // Load texture immediately
                    _grassSpriteTexture = await TextureLoader.Instance.PrepareAndGetTexture(_grassSpritePath);
                    if (_grassSpriteTexture != null)
                    {
                        // Important: do premultiply only once per texture asset
                        bool doPremul = false;
                        lock (_premulLock)
                        {
                            if (!_premultipliedOnce.Contains(_grassSpritePath))
                            {
                                _premultipliedOnce.Add(_grassSpritePath);
                                doPremul = true;
                            }
                        }
                        if (doPremul)
                        {
                            PremultiplyAlpha(_grassSpriteTexture);
                        }
                        _texReady = true;
                    }
                    else
                        Console.WriteLine($"Warning: Could not load grass sprite texture yet (queued): {_grassSpritePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading grass sprite texture '{_grassSpritePath}': {ex.Message}");
                }
                // Use a dedicated effect instance to avoid state interference
                _grassEffect = new AlphaTestEffect(_graphicsDevice);
            }
        }

        /// <summary>
        /// Ensures grass content is loaded when DRAW_GRASS is enabled after initial load.
        /// Call this when toggling grass setting from false to true.
        /// </summary>
        public void EnsureContentLoaded(short worldIndex)
        {
            if (Constants.DRAW_GRASS && !_texReady)
            {
                LoadContent(worldIndex);
            }
        }

        public void ResetMetrics()
        {
            Flushes = 0;
            DrawnTriangles = 0;
        }

        public void RenderGrassForTile(int xi, int yi, float xf, float yf, float lodFactor, short worldIndex)
        {
            if (!Constants.DRAW_GRASS
                || worldIndex == 11
                || !_texReady)
                return;

            // Be robust with LOD: when rendering a super-tile (lodFactor > 1),
            // check underlying sub-tiles instead of a single top-left sample.
            if (!IsGrassAllowedForTile(xi, yi, lodFactor))
                return;

            var camPos = Camera.Instance.Position;
            float tileCx = (xf + 0.5f * lodFactor) * Constants.TERRAIN_SCALE;
            float tileCy = (yf + 0.5f * lodFactor) * Constants.TERRAIN_SCALE;
            float dx = camPos.X - tileCx, dy = camPos.Y - tileCy;
            float distSq = dx * dx + dy * dy;

            int grassPerTile = GrassCount(distSq, lodFactor);
            if (grassPerTile == 0) return;

            // Optimization: Simple distance-based culling (skip very far tiles)
            if (distSq > GrassFarSq * 1.5f) // Skip tiles beyond 1.5x far distance
                return;

            int terrainIndex = yi * Constants.TERRAIN_SIZE + xi;
            var staticLight = terrainIndex < _data.FinalLightMap.Length
                          ? _data.FinalLightMap[terrainIndex]
                          : Color.White;
            float windBase = _wind.GetWindValue(xi, yi);

            // Calculate dynamic light once per tile (tile center)
            var dynTile = _lightManager?.EvaluateDynamicLight(new Vector2(tileCx, tileCy)) ?? Vector3.Zero;
            var combined = new Vector3(staticLight.R, staticLight.G, staticLight.B) + dynTile;
            combined = Vector3.Clamp(combined, Vector3.Zero, new Vector3(255f));
            var tileLight = new Color(
                (byte)MathF.Min(combined.X * GrassBrightness, 255f),
                (byte)MathF.Min(combined.Y * GrassBrightness, 255f),
                (byte)MathF.Min(combined.Z * GrassBrightness, 255f));

            const float GrassUWidth = 0.30f;
            const float ScaleMin = 1.0f;
            const float ScaleMax = 3.0f;
            const float RotJitterDeg = 90f;
            const float HeightOffset = 55f;

            // Optimization: Pre-calculate common values
            const float windRadians = 0.35f;
            float windZBase = MathHelper.ToRadians(windBase * windRadians);

            for (int i = 0; i < grassPerTile; i++)
            {
                // Use cached random values for better performance
                float u0 = PseudoRandom(xi, yi, 123 + i) * (1f - GrassUWidth);
                float u1 = u0 + GrassUWidth;
                float halfUV = GrassUWidth * 0.5f;
                float maxOffset = 0.5f - halfUV;

                float rx = (PseudoRandom(xi, yi, 17 + i) * 2f - 1f) * maxOffset;
                float ry = (PseudoRandom(xi, yi, 91 + i) * 2f - 1f) * maxOffset;

                float worldX = (xf + 0.5f * lodFactor + rx * lodFactor) * Constants.TERRAIN_SCALE;
                float worldY = (yf + 0.5f * lodFactor + ry * lodFactor) * Constants.TERRAIN_SCALE;
                float h = _physics.RequestTerrainHeight(worldX, worldY);

                float scale = MathHelper.Lerp(ScaleMin, ScaleMax, PseudoRandom(xi, yi, 33 + i));
                float jitter = MathHelper.ToRadians((PseudoRandom(xi, yi, 57 + i) - 0.5f) * 2f * RotJitterDeg);
                float windZ = windZBase + jitter;

                // Keep grass size roughly constant across LODs: compensate by 1/lodFactor
                float sizeFactor = scale / MathF.Max(1f, lodFactor);

                RenderGrassQuad(
                    new Vector3(worldX, worldY, h + HeightOffset),
                    sizeFactor,
                    windZ,
                    tileLight,
                    u0, u1
                );
            }
        }

        private bool IsGrassAllowedForTile(int xi, int yi, float lodFactor)
        {
            byte baseTex = _physics.GetBaseTextureIndexAt(xi, yi);
            return GrassTextureIndices.Contains(baseTex);
        }

        private void RenderGrassQuad(
            Vector3 position,
            float lodFactor,
            float windRotationZ,
            Color lightColor,
            float u0,
            float u1)
        {
            const float BaseW = 130f, BaseH = 45f;
            float w = BaseW * (u1 - u0) * lodFactor;
            float h = BaseH * lodFactor;
            float hw = w * 0.5f;

            // Fast rotation without matrices: Z-rot by 45° for bottom and (45°+wind) for top
            float cosA = Cos45, sinA = Sin45;
            float angB = MathHelper.ToRadians(45f) + windRotationZ;
            float cosB = MathF.Cos(angB), sinB = MathF.Sin(angB);

            // Bottom vertices (no wind)
            var wp1 = new Vector3(position.X + (-hw) * cosA, position.Y + (-hw) * sinA, position.Z);
            var wp2 = new Vector3(position.X + (hw) * cosA, position.Y + (hw) * sinA, position.Z);
            // Top vertices (with wind)
            var wp3 = new Vector3(position.X + (-hw) * cosB, position.Y + (-hw) * sinB, position.Z + h);
            var wp4 = new Vector3(position.X + (hw) * cosB, position.Y + (hw) * sinB, position.Z + h);

            var t1 = new Vector2(u0, 1);
            var t2 = new Vector2(u1, 1);
            var t3 = new Vector2(u0, 0);
            var t4 = new Vector2(u1, 0);

            if (_grassBatchCount + 6 >= GrassBatchVerts)
                Flush();

            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp1, lightColor, t1);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp2, lightColor, t2);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp3, lightColor, t3);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp2, lightColor, t2);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp4, lightColor, t4);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp3, lightColor, t3);
        }

        public void Flush()
        {
            if (!Constants.DRAW_GRASS
                || _grassBatchCount == 0
                || !_texReady
                || _grassEffect == null)
                return;


            var dev = _graphicsDevice;
            var prevBlend = dev.BlendState;
            var prevDepth = dev.DepthStencilState;
            var prevRaster = dev.RasterizerState;
            var prevSampler = dev.SamplerStates[0];

            dev.BlendState = BlendState.AlphaBlend;
            dev.DepthStencilState = DepthStencilState.Default;
            dev.RasterizerState = RasterizerState.CullNone;
            dev.SamplerStates[0] = SamplerState.PointClamp;

            // Don't block rendering thread with .GetResult() - only draw when _texReady

            _grassEffect.World = Matrix.Identity;
            _grassEffect.View = Camera.Instance.View;
            _grassEffect.Projection = Camera.Instance.Projection;
            _grassEffect.Texture = _grassSpriteTexture;
            _grassEffect.Alpha = 1f; // Force full vertex/diffuse alpha to avoid accidental fade
            _grassEffect.AlphaFunction = CompareFunction.Greater;
            _grassEffect.ReferenceAlpha = 64;
            _grassEffect.VertexColorEnabled = true;

            int triCount = _grassBatchCount / 3;
            if (_grassEffect.CurrentTechnique == null) return; // Added null check for CurrentTechnique
            foreach (var pass in _grassEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                dev.DrawUserPrimitives(PrimitiveType.TriangleList, _grassBatch, 0, triCount);
                DrawnTriangles += triCount;
            }

            Flushes++;
            _grassBatchCount = 0;

            dev.BlendState = prevBlend;
            dev.DepthStencilState = prevDepth;
            dev.RasterizerState = prevRaster;
            dev.SamplerStates[0] = prevSampler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GrassCount(float distSq, float lodFactor = 1.0f)
        {
            int baseCount;
            if (distSq < GrassNearSq) baseCount = 10;
            else if (distSq < GrassMidSq) baseCount = 4;
            else if (distSq < GrassFarSq) baseCount = 2;
            else return 0;

            // Reduce grass count for higher LOD levels
            return lodFactor > 1.0f ? Math.Max(1, baseCount / 2) : baseCount;
        }

        private static void PremultiplyAlpha(Texture2D tex)
        {
            if (tex.Format != SurfaceFormat.Color || tex.IsDisposed) return;
            int len = tex.Width * tex.Height;
            var px = new Color[len];
            tex.GetData(px);
            for (int i = 0; i < len; i++)
            {
                var c = px[i];
                if (c.A == 255) continue;
                px[i] = new Color(
                    (byte)(c.R * c.A / 255),
                    (byte)(c.G * c.A / 255),
                    (byte)(c.B * c.A / 255),
                    c.A);
            }
            tex.SetData(px);
        }


        // 32-bit Xorshift* hash → float [0..1]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float PseudoRandom(int x, int y, int salt = 0)
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
            h ^= h >> 13; h *= 0x165667B1u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }
}
