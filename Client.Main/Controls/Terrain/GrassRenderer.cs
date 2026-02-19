using Client.Main.Content;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Renders grass using static GPU chunks. Geometry is built once per world load,
    /// while wind deformation is applied in Grass.fx on GPU.
    /// </summary>
    public sealed class GrassRenderer : IDisposable
    {
        private sealed class GrassChunk : IDisposable
        {
            public VertexBuffer VertexBuffer;
            public int VertexCount;
            public BoundingBox Bounds;
            public Vector3 Center;

            public void Dispose()
            {
                VertexBuffer?.Dispose();
                VertexBuffer = null;
            }
        }

        private const float GrassBladeBaseW = 130f;
        private const float GrassBladeBaseH = 45f;
        private const float GrassScaleMax = 3.0f;
        private const int ChunkSize = 16; // 16x16 tiles
        private const int GrassPerTile = 6;
        private const float MaxRenderDistanceSq = 5000f * 5000f;

        private volatile bool _texReady;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly TerrainData _data;
        private readonly TerrainPhysics _physics;
        private readonly WindSimulator _wind;

        private Texture2D _grassSpriteTexture;
        private Effect _grassWindEffect;
        private string _grassSpritePath;
        private short _worldIndex;

        private readonly List<GrassChunk> _chunks = new();

        public float GrassBrightness { get; set; } = 2f;
        public HashSet<byte> GrassTextureIndices { get; } = new() { 0 };
        public int Flushes { get; private set; }
        public int DrawnTriangles { get; private set; }

        public GrassRenderer(
            GraphicsDevice graphicsDevice,
            TerrainData data,
            TerrainPhysics physics,
            WindSimulator wind,
            TerrainLightManager lightManager)
        {
            _graphicsDevice = graphicsDevice;
            _data = data;
            _physics = physics;
            _wind = wind;
        }

        private static readonly object _premulLock = new();
        private static readonly HashSet<string> _premultipliedOnce = new(StringComparer.OrdinalIgnoreCase);

        public async void LoadContent(short worldIndex)
        {
            if (!Constants.DRAW_GRASS)
                return;

            _worldIndex = worldIndex;
            string textureFile = worldIndex == 3 ? "TileGrass02.ozt" : "TileGrass01.ozt";
            _grassSpritePath = Path.Combine($"World{worldIndex}", textureFile);

            try
            {
                _grassSpriteTexture = await TextureLoader.Instance.PrepareAndGetTexture(_grassSpritePath);
                if (_grassSpriteTexture != null)
                {
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
                        PremultiplyAlpha(_grassSpriteTexture);

                    _texReady = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading grass texture '{_grassSpritePath}': {ex.Message}");
            }

            try
            {
                _grassWindEffect ??= MuGame.Instance?.Content?.Load<Effect>("Grass");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading grass shader effect: {ex.Message}");
                _grassWindEffect = null;
            }
        }

        public void EnsureContentLoaded(short worldIndex)
        {
            if (Constants.DRAW_GRASS && !_texReady)
                LoadContent(worldIndex);
        }

        public void ResetMetrics()
        {
            Flushes = 0;
            DrawnTriangles = 0;
        }

        /// <summary>
        /// Builds all grass chunks once and uploads static buffers to GPU.
        /// </summary>
        public void BuildAllGrass()
        {
            for (int i = 0; i < _chunks.Count; i++)
                _chunks[i].Dispose();
            _chunks.Clear();

            if (!Constants.DRAW_GRASS || _worldIndex == 11)
                return;

            int chunksX = Constants.TERRAIN_SIZE / ChunkSize;
            int chunksY = Constants.TERRAIN_SIZE / ChunkSize;

            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var chunk = BuildChunk(cx, cy);
                    if (chunk != null)
                        _chunks.Add(chunk);
                }
            }
        }

        private GrassChunk BuildChunk(int chunkX, int chunkY)
        {
            var vertices = new List<GrassVertexPositionColorTextureWind>(ChunkSize * ChunkSize * GrassPerTile * 6);
            Vector3 minBounds = new Vector3(float.MaxValue);
            Vector3 maxBounds = new Vector3(float.MinValue);

            int startX = chunkX * ChunkSize;
            int startY = chunkY * ChunkSize;
            int endX = startX + ChunkSize;
            int endY = startY + ChunkSize;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if (!GrassTextureIndices.Contains(_physics.GetBaseTextureIndexAt(x, y)))
                        continue;

                    int terrainIndex = y * Constants.TERRAIN_SIZE + x;
                    Color staticLight = Color.White;
                    if (_data.FinalLightMap != null &&
                        terrainIndex >= 0 &&
                        terrainIndex < _data.FinalLightMap.Length)
                    {
                        staticLight = _data.FinalLightMap[terrainIndex];
                    }

                    var tileLight = new Color(
                        (byte)MathF.Min(staticLight.R * GrassBrightness, 255f),
                        (byte)MathF.Min(staticLight.G * GrassBrightness, 255f),
                        (byte)MathF.Min(staticLight.B * GrassBrightness, 255f));

                    float windBase = _wind.GetWindValue(x, y);
                    float windZBase = MathHelper.ToRadians(windBase * 0.35f);

                    for (int i = 0; i < GrassPerTile; i++)
                        AddGrassBlade(vertices, x, y, i, windZBase, tileLight, ref minBounds, ref maxBounds);
                }
            }

            if (vertices.Count == 0)
                return null;

            var chunk = new GrassChunk
            {
                VertexCount = vertices.Count,
                Bounds = new BoundingBox(minBounds, maxBounds),
                Center = (minBounds + maxBounds) * 0.5f,
                VertexBuffer = new VertexBuffer(
                    _graphicsDevice,
                    GrassVertexPositionColorTextureWind.VertexDeclaration,
                    vertices.Count,
                    BufferUsage.WriteOnly)
            };

            chunk.VertexBuffer.SetData(vertices.ToArray());
            return chunk;
        }

        private void AddGrassBlade(
            List<GrassVertexPositionColorTextureWind> vertices,
            int tileX,
            int tileY,
            int bladeIndex,
            float windZBase,
            Color lightColor,
            ref Vector3 minBounds,
            ref Vector3 maxBounds)
        {
            const float GrassUWidth = 0.30f;

            float u0 = PseudoRandom(tileX, tileY, 123 + bladeIndex) * (1f - GrassUWidth);
            float u1 = u0 + GrassUWidth;
            float maxOffset = 0.5f - (GrassUWidth * 0.5f);

            float rx = (PseudoRandom(tileX, tileY, 17 + bladeIndex) * 2f - 1f) * maxOffset;
            float ry = (PseudoRandom(tileX, tileY, 91 + bladeIndex) * 2f - 1f) * maxOffset;

            float worldX = (tileX + 0.5f + rx) * Constants.TERRAIN_SCALE;
            float worldY = (tileY + 0.5f + ry) * Constants.TERRAIN_SCALE;
            float h = _physics.RequestTerrainHeight(worldX, worldY);

            float scale = MathHelper.Lerp(1.0f, GrassScaleMax, PseudoRandom(tileX, tileY, 33 + bladeIndex));
            float jitter = MathHelper.ToRadians((PseudoRandom(tileX, tileY, 57 + bladeIndex) - 0.5f) * 2f * 90f);
            float windRotationZ = windZBase + jitter;

            float w = GrassBladeBaseW * (u1 - u0) * scale;
            float bladeHeight = GrassBladeBaseH * scale;
            float hw = w * 0.5f;

            float baseAngle = MathHelper.ToRadians(45f) + windRotationZ;
            float cosBase = MathF.Cos(baseAngle);
            float sinBase = MathF.Sin(baseAngle);

            Vector3 basePos = new Vector3(worldX, worldY, h + 55f);

            Vector3 wp1 = new Vector3(basePos.X + (-hw) * cosBase, basePos.Y + (-hw) * sinBase, basePos.Z);
            Vector3 wp2 = new Vector3(basePos.X + (hw) * cosBase, basePos.Y + (hw) * sinBase, basePos.Z);
            Vector3 wp3 = new Vector3(wp1.X, wp1.Y, basePos.Z + bladeHeight);
            Vector3 wp4 = new Vector3(wp2.X, wp2.Y, basePos.Z + bladeHeight);

            minBounds = Vector3.Min(minBounds, wp1);
            minBounds = Vector3.Min(minBounds, wp2);
            minBounds = Vector3.Min(minBounds, wp3);
            minBounds = Vector3.Min(minBounds, wp4);
            maxBounds = Vector3.Max(maxBounds, wp1);
            maxBounds = Vector3.Max(maxBounds, wp2);
            maxBounds = Vector3.Max(maxBounds, wp3);
            maxBounds = Vector3.Max(maxBounds, wp4);

            Vector2 t1 = new Vector2(u0, 1f);
            Vector2 t2 = new Vector2(u1, 1f);
            Vector2 t3 = new Vector2(u0, 0f);
            Vector2 t4 = new Vector2(u1, 0f);

            float dirX = -sinBase;
            float dirY = cosBase;
            float phase = windRotationZ * 2.7f + basePos.X * 0.0012f + basePos.Y * 0.0011f;
            float swayAmplitude = MathF.Max(6f, bladeHeight * 0.22f);

            Vector4 windBottom = new Vector4(dirX, dirY, phase, 0f);
            Vector4 windTop = new Vector4(dirX, dirY, phase, swayAmplitude);

            vertices.Add(new GrassVertexPositionColorTextureWind(wp1, lightColor, t1, windBottom));
            vertices.Add(new GrassVertexPositionColorTextureWind(wp2, lightColor, t2, windBottom));
            vertices.Add(new GrassVertexPositionColorTextureWind(wp3, lightColor, t3, windTop));
            vertices.Add(new GrassVertexPositionColorTextureWind(wp2, lightColor, t2, windBottom));
            vertices.Add(new GrassVertexPositionColorTextureWind(wp4, lightColor, t4, windTop));
            vertices.Add(new GrassVertexPositionColorTextureWind(wp3, lightColor, t3, windTop));
        }

        public void Draw()
        {
            if (!Constants.DRAW_GRASS ||
                _worldIndex == 11 ||
                !_texReady ||
                _grassWindEffect == null ||
                _grassSpriteTexture == null ||
                _chunks.Count == 0)
            {
                return;
            }

            var dev = _graphicsDevice;
            var prevBlend = dev.BlendState;
            var prevDepth = dev.DepthStencilState;
            var prevRaster = dev.RasterizerState;
            var prevSampler = dev.SamplerStates[0];

            dev.BlendState = BlendState.AlphaBlend;
            dev.DepthStencilState = DepthStencilState.Default;
            dev.RasterizerState = RasterizerState.CullNone;
            dev.SamplerStates[0] = SamplerState.PointClamp;

            float timeSeconds = (float)(MuGame.Instance?.GameTime.TotalGameTime.TotalSeconds ?? 0.0);
            _grassWindEffect.Parameters["World"]?.SetValue(Matrix.Identity);
            _grassWindEffect.Parameters["View"]?.SetValue(Camera.Instance.View);
            _grassWindEffect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
            _grassWindEffect.Parameters["GrassTexture"]?.SetValue(_grassSpriteTexture);
            _grassWindEffect.Parameters["Time"]?.SetValue(timeSeconds);
            _grassWindEffect.Parameters["WindSpeed"]?.SetValue(2.2f);
            _grassWindEffect.Parameters["WindStrength"]?.SetValue(1.0f);
            _grassWindEffect.Parameters["AlphaCutoff"]?.SetValue(64f / 255f);

            var frustum = Camera.Instance.Frustum;
            var camPos = Camera.Instance.Position;

            if (_grassWindEffect.CurrentTechnique != null)
            {
                foreach (var pass in _grassWindEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    for (int i = 0; i < _chunks.Count; i++)
                    {
                        var chunk = _chunks[i];
                        if (Vector3.DistanceSquared(camPos, chunk.Center) > MaxRenderDistanceSq)
                            continue;

                        if (frustum.Contains(chunk.Bounds) == ContainmentType.Disjoint)
                            continue;

                        dev.SetVertexBuffer(chunk.VertexBuffer);
                        dev.DrawPrimitives(PrimitiveType.TriangleList, 0, chunk.VertexCount / 3);

                        DrawnTriangles += chunk.VertexCount / 3;
                        Flushes++;
                    }
                }
            }

            dev.SetVertexBuffer(null);
            dev.BlendState = prevBlend;
            dev.DepthStencilState = prevDepth;
            dev.RasterizerState = prevRaster;
            dev.SamplerStates[0] = prevSampler;
        }

        public void Dispose()
        {
            for (int i = 0; i < _chunks.Count; i++)
                _chunks[i].Dispose();
            _chunks.Clear();

            _grassWindEffect = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float PseudoRandom(int x, int y, int salt = 0)
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
            h ^= h >> 13;
            h *= 0x165667B1u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }

        private static void PremultiplyAlpha(Texture2D tex)
        {
            if (tex.Format != SurfaceFormat.Color || tex.IsDisposed)
                return;

            int len = tex.Width * tex.Height;
            var px = new Color[len];
            tex.GetData(px);

            for (int i = 0; i < len; i++)
            {
                var c = px[i];
                if (c.A == 255)
                    continue;

                px[i] = new Color(
                    (byte)(c.R * c.A / 255),
                    (byte)(c.G * c.A / 255),
                    (byte)(c.B * c.A / 255),
                    c.A);
            }

            tex.SetData(px);
        }
    }
}
