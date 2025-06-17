using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Main.Content;
using Client.Main.Controllers;
using static Client.Main.Utils;

namespace Client.Main.Controls
{
    public class TerrainControl : GameControl
    {
        // Constants
        private const float SpecialHeight = 1200f;
        private const int BlockSize = 4;
        private const int MaxLodLevels = 2;
        private const float LodDistanceMultiplier = 3000f;
        private const float WindScale = 10f;
        private const int UpdateIntervalMs = 32;
        private const float CameraMoveThreshold = 32f;
        /// <summary>
        /// Texture indices that should spawn grass tufts.
        /// </summary>
        public HashSet<byte> GrassTextureIndices { get; } = new() { 0, 1, 30, 31, 32 };

        // Grass density distances (squared)
        private const float GrassNearSq = 3000f * 3000f;   // full density
        private const float GrassMidSq = 4000f * 4000f;   // two tufts
        private const float GrassFarSq = 5000f * 5000f;   // one tuft
        private const int GrassBatchQuads = 16384;       // 4096 tufts per batch
        private const int GrassBatchVerts = GrassBatchQuads * 6;

        private const int TileBatchVerts = 4096 * 6;       // 4096 tiles = 24k vertices
        private const int TileBatchQuads = 256;            // quads per texture batch

        // Per-texture tile batches
        private readonly VertexPositionColorTexture[][] _tileBatches = new VertexPositionColorTexture[256][];
        private readonly int[] _tileBatchCounts = new int[256];
        private readonly VertexPositionColorTexture[] _tileBatch = new VertexPositionColorTexture[TileBatchVerts];

        private readonly VertexPositionColorTexture[][] _tileAlphaBatches = new VertexPositionColorTexture[256][];
        private readonly int[] _tileAlphaCounts = new int[256];

        // Public properties
        public short WorldIndex { get; set; }
        public Vector3 Light { get; set; } = new Vector3(0.5f, -0.5f, 0.5f);

        // Only lights currently in range
        private readonly List<DynamicLight> _activeLights = new(32);

        public Dictionary<int, string> TextureMappingFiles { get; set; } = new Dictionary<int, string>
        {
            {   0, "TileGrass01.ozj" },
            {   1, "TileGrass02.ozj" },
            {   2, "TileGround01.ozj" },
            {   3, "TileGround02.ozj" },
            {   4, "TileGround03.ozj" },
            {   5, "TileWater01.ozj" },
            {   6, "TileWood01.ozj" },
            {   7, "TileRock01.ozj" },
            {   8, "TileRock02.ozj" },
            {   9, "TileRock03.ozj" },
            {  10, "TileRock04.ozj" },
            {  11, "TileRock05.ozj" },
            {  12, "TileRock06.ozj" },
            {  13, "TileRock07.ozj" },
            {  30, "TileGrass01.ozt" },
            {  31, "TileGrass02.ozt" },
            {  32, "TileGrass03.ozt" },
            { 100, "leaf01.ozt" },
            { 101, "leaf02.ozj" },
            { 102, "rain01.ozt" },
            { 103, "rain02.ozt" },
            { 104, "rain03.ozt" }
        };

        public float WaterSpeed { get; set; } = 0f;    // water animation speed factor
        public float DistortionAmplitude { get; set; } = 0f;    // water UV distortion amplitude
        public float DistortionFrequency { get; set; } = 0f;    // water UV distortion frequency
        public float GrassBrightness { get; set; } = 2f;
        public float AmbientLight { get; set; } = 0.25f;

        // Private terrain data
        private TerrainAttribute _terrain;
        private TerrainMapping _mapping;
        private Texture2D[] _textures;
        private float[] _terrainGrassWind;
        private Color[] _backTerrainLight;
        private Vector3[] _terrainNormal;
        private Color[] _backTerrainHeight;
        private Color[] _terrainLightData;

        private Vector2 _waterFlowDir = Vector2.UnitX;
        private float _waterTotal = 0f; // continuous accumulator for water UV offset

        // Grass rendering resources
        private Texture2D _grassSpriteTexture;
        private AlphaTestEffect _grassEffect;

        // Buffers for a single terrain tile quad
        private readonly VertexPositionColorTexture[] _terrainVertices = new VertexPositionColorTexture[6];
        private readonly Vector2[] _terrainTextureCoords = new Vector2[4];
        private readonly Vector3[] _tempTerrainVertex = new Vector3[4];
        private readonly Color[] _tempTerrainLights = new Color[4];

        // Dynamic lights
        private readonly List<DynamicLight> _dynamicLights = new();
        public IReadOnlyList<DynamicLight> DynamicLights => _dynamicLights;

        // Wind simulation
        private float _lastWindSpeed = float.MinValue;
        private double _lastUpdateTime;
        private readonly WindCache _windCache = new();

        // LOD and culling
        private readonly int[] _lodSteps = { 1, 4 };
        private readonly TerrainBlockCache _blockCache;
        private readonly Queue<TerrainBlock> _visibleBlocks = new(64);
        private Vector2 _lastCameraPosition;

        // Grass batching
        private readonly VertexPositionColorTexture[] _grassBatch = new VertexPositionColorTexture[GrassBatchVerts];
        private int _grassBatchCount = 0;

        public sealed class TerrainFrameMetrics
        {
            public int DrawCalls;
            public int DrawnTriangles;
            public int DrawnBlocks;
            public int DrawnCells;
            public int GrassFlushes;

            public void Reset()
            {
                DrawCalls = 0;
                DrawnTriangles = 0;
                DrawnBlocks = 0;
                DrawnCells = 0;
                GrassFlushes = 0;
            }
        }

        // The property name remains FrameMetrics, but its type is TerrainFrameMetrics
        public TerrainFrameMetrics FrameMetrics { get; } = new TerrainFrameMetrics();

        // Ensures water flow direction is normalized
        public Vector2 WaterFlowDirection
        {
            get => _waterFlowDir;
            set => _waterFlowDir = value.LengthSquared() < 1e-4f
                                   ? Vector2.UnitX
                                   : Vector2.Normalize(value);
        }

        public Texture2D HeightMapTexture { get; private set; }

        public TerrainControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);

            _blockCache = new TerrainBlockCache(BlockSize, Constants.TERRAIN_SIZE);

            // Initialize per-texture batches
            for (int i = 0; i < 256; i++)
            {
                _tileBatches[i] = new VertexPositionColorTexture[TileBatchVerts];
                _tileAlphaBatches[i] = new VertexPositionColorTexture[TileBatchVerts];
            }
        }

        public override async Task Load()
        {
            var terrainReader = new ATTReader();
            var ozbReader = new OZBReader();
            var objReader = new OBJReader();
            var mappingReader = new MapReader();
            var bmdReader = new BMDReader();

            var tasks = new List<Task>();
            var worldFolder = $"World{WorldIndex}";
            var fullPathWorldFolder = Path.Combine(Constants.DataPath, worldFolder);

            if (string.IsNullOrEmpty(fullPathWorldFolder) || !Directory.Exists(fullPathWorldFolder))
                return;

            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            // Load terrain .att
            string attPath = GetActualPath(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att"));
            if (!string.IsNullOrEmpty(attPath))
                tasks.Add(terrainReader.Load(attPath).ContinueWith(t => _terrain = t.Result));

            // Load base terrain height map
            string heightPath = GetActualPath(Path.Combine(fullPathWorldFolder, "TerrainHeight.OZB"));
            if (!string.IsNullOrEmpty(heightPath))
                tasks.Add(ozbReader.Load(heightPath)
                    .ContinueWith(t => _backTerrainHeight = t.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));

            // Load terrain mapping (.map)
            string mapPath = GetActualPath(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map"));
            if (!string.IsNullOrEmpty(mapPath))
                tasks.Add(mappingReader.Load(mapPath).ContinueWith(t => _mapping = t.Result));

            // Prepare texture file list
            var textureMapFiles = new string[256];
            foreach (var kvp in TextureMappingFiles)
            {
                textureMapFiles[kvp.Key] = GetActualPath(Path.Combine(fullPathWorldFolder, kvp.Value));
            }
            for (int i = 1; i <= 16; i++)
            {
                var extTilePath = GetActualPath(Path.Combine(fullPathWorldFolder, $"ExtTile{i:00}.ozj"));
                textureMapFiles[13 + i] = extTilePath;
            }

            _textures = new Texture2D[textureMapFiles.Length];
            for (int t = 0; t < textureMapFiles.Length; t++)
            {
                var path = textureMapFiles[t];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                int textureIndex = t;
                tasks.Add(TextureLoader.Instance.Prepare(path)
                    .ContinueWith(_ => _textures[textureIndex] = TextureLoader.Instance.GetTexture2D(path)));
            }

            // Load lightmap or default to white
            string textureLightPath = GetActualPath(Path.Combine(fullPathWorldFolder, "TerrainLight.OZB"));
            if (!string.IsNullOrEmpty(textureLightPath) && File.Exists(textureLightPath))
            {
                tasks.Add(ozbReader.Load(textureLightPath)
                    .ContinueWith(ozb => _terrainLightData = ozb.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            }
            else
            {
                _terrainLightData = Enumerable.Repeat(Color.White, Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE).ToArray();
            }

            await Task.WhenAll(tasks);

            _terrainGrassWind = new float[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            PrecomputeBlockHeights();
            CreateTerrainNormal();
            CreateTerrainLight();
            CreateHeightMapTexture();

            if (Constants.DRAW_GRASS)
            {
                var grassSpritePath = Path.Combine(worldFolder, "TileGrass01.ozt");
                try
                {
                    _grassSpriteTexture = await TextureLoader.Instance.PrepareAndGetTexture(grassSpritePath);
                    if (_grassSpriteTexture != null)
                        PremultiplyAlpha(_grassSpriteTexture);
                    else
                        Console.WriteLine($"Warning: Could not load grass sprite texture: {grassSpritePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading grass sprite texture '{grassSpritePath}': {ex.Message}");
                }

                _grassEffect = GraphicsManager.Instance.AlphaTestEffect3D;
            }

            await base.Load();
        }

        public override void Update(GameTime time)
        {
            Thread.Sleep(0);
            base.Update(time);

            if (Status != Models.GameControlStatus.Ready)
                return;

            var camPos2D = new Vector2(Camera.Instance.Position.X, Camera.Instance.Position.Y);
            UpdateVisibleBlocks(camPos2D);
            InitTerrainWind(time);

            // Advance water UV offset
            _waterTotal += (float)time.ElapsedGameTime.TotalSeconds * WaterSpeed;
        }

        public override void Draw(GameTime time)
        {
            if (!Visible || Status != Models.GameControlStatus.Ready)
                return;

            RenderTerrain(false);
            base.Draw(time);
        }

        public override void DrawAfter(GameTime gameTime)
        {
            RenderTerrain(true);
            base.DrawAfter(gameTime);
        }

        public TWFlags RequestTerrainFlag(int x, int y)
            => _terrain.TerrainWall[GetTerrainIndex(x, y)];

        public float RequestTerrainHeight(float xf, float yf)
        {
            if (_terrain?.TerrainWall == null
                || xf < 0 || yf < 0
                || _backTerrainHeight == null
                || float.IsNaN(xf) || float.IsNaN(yf))
                return 0f;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf, yi = (int)yf;
            int index = GetTerrainIndex(xi, yi);

            // If flagged as special height, return SpecialHeight
            if (index < _terrain.TerrainWall.Length
                && _terrain.TerrainWall[index].HasFlag(TWFlags.Height))
                return SpecialHeight;

            // Bilinear interpolation of height
            float xd = xf - xi, yd = yf - yi;
            int x1 = xi & Constants.TERRAIN_SIZE_MASK, y1 = yi & Constants.TERRAIN_SIZE_MASK;
            int x2 = (xi + 1) & Constants.TERRAIN_SIZE_MASK, y2 = (yi + 1) & Constants.TERRAIN_SIZE_MASK;

            int i1 = y1 * Constants.TERRAIN_SIZE + x1;
            int i2 = y1 * Constants.TERRAIN_SIZE + x2;
            int i3 = y2 * Constants.TERRAIN_SIZE + x2;
            int i4 = y2 * Constants.TERRAIN_SIZE + x1;

            float h1 = _backTerrainHeight[i1].B;
            float h2 = _backTerrainHeight[i2].B;
            float h3 = _backTerrainHeight[i3].B;
            float h4 = _backTerrainHeight[i4].B;

            return (1 - xd) * (1 - yd) * h1
                 + xd * (1 - yd) * h2
                 + xd * yd * h3
                 + (1 - xd) * yd * h4;
        }

        private void CreateHeightMapTexture()
        {
            if (_backTerrainHeight == null || GraphicsDevice == null) return;

            HeightMapTexture = new Texture2D(GraphicsDevice, Constants.TERRAIN_SIZE, Constants.TERRAIN_SIZE, false, SurfaceFormat.Single);

            float[] heightData = ArrayPool<float>.Shared.Rent(Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE);
            try
            {
                for (int i = 0; i < heightData.Length; i++)
                {
                    heightData[i] = _backTerrainHeight[i].B / 255.0f;
                }
                HeightMapTexture.SetData(heightData);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(heightData);
            }
        }

        public Vector3 RequestTerrainLight(float xf, float yf)
        {
            if (_terrain?.TerrainWall == null
                || xf < 0 || yf < 0
                || _backTerrainLight == null)
                return Vector3.One;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf, yi = (int)yf;
            float xd = xf - xi, yd = yf - yi;

            int i1 = xi + yi * Constants.TERRAIN_SIZE;
            int i2 = (xi + 1) + yi * Constants.TERRAIN_SIZE;
            int i3 = (xi + 1) + (yi + 1) * Constants.TERRAIN_SIZE;
            int i4 = xi + (yi + 1) * Constants.TERRAIN_SIZE;

            if (new[] { i1, i2, i3, i4 }.Any(i => i < 0 || i >= _backTerrainLight.Length))
                return Vector3.Zero;

            float[] rgb = new float[3];
            for (int c = 0; c < 3; c++)
            {
                float left = MathHelper.Lerp(GetChannel(_backTerrainLight[i1], c),
                                              GetChannel(_backTerrainLight[i4], c),
                                              yd);
                float right = MathHelper.Lerp(GetChannel(_backTerrainLight[i2], c),
                                              GetChannel(_backTerrainLight[i3], c),
                                              yd);
                rgb[c] = MathHelper.Lerp(left, right, xd);
            }

            var result = new Vector3(rgb[0], rgb[1], rgb[2])
                       + new Vector3(AmbientLight * 255f)
                       + EvaluateDynamicLight(new Vector2(xf * Constants.TERRAIN_SCALE, yf * Constants.TERRAIN_SCALE));
            result = Vector3.Clamp(result, Vector3.Zero, new Vector3(255f));
            return result / 255f;
        }

        private static byte GetChannel(Color c, int index)
        {
            return index switch
            {
                0 => c.R,
                1 => c.G,
                2 => c.B,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private Vector3 EvaluateDynamicLight(Vector2 position)
        {
            Vector3 result = Vector3.Zero;
            foreach (var light in _activeLights)
            {
                if (light.Intensity <= 0.001f) continue;

                var diff = new Vector2(light.Position.X, light.Position.Y) - position;
                float distSq = diff.LengthSquared();
                float radiusSq = light.Radius * light.Radius;
                if (distSq > radiusSq) continue;

                float dist = MathF.Sqrt(distSq);
                float factor = 1f - dist / light.Radius;
                result += light.Color * (255f * light.Intensity * factor);
            }
            return result;
        }

        public void AddDynamicLight(DynamicLight light) => _dynamicLights.Add(light);
        public void RemoveDynamicLight(DynamicLight light) => _dynamicLights.Remove(light);

        public float GetWindValue(int x, int y)
            => _terrainGrassWind[y * Constants.TERRAIN_SIZE + x];

        public override void Dispose()
        {
            base.Dispose();
            _terrain = null;
            _mapping = default;
            _textures = null;
            _terrainGrassWind = null;
            _backTerrainLight = null;
            _terrainNormal = null;
            _backTerrainHeight = null;
            GC.SuppressFinalize(this);
        }

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;

        private static int GetTerrainIndexRepeat(int x, int y)
            => ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE)
             + (x & Constants.TERRAIN_SIZE_MASK);

        private void CreateTerrainNormal()
        {
            _terrainNormal = new Vector3[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int i = GetTerrainIndex(x, y);
                    var v1 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE,
                        _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y)].B);
                    var v2 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE,
                        _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y + 1)].B);
                    var v3 = new Vector3(x * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE,
                        _backTerrainHeight[GetTerrainIndexRepeat(x, y + 1)].B);
                    var v4 = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE,
                        _backTerrainHeight[GetTerrainIndexRepeat(x, y)].B);

                    var n1 = MathUtils.FaceNormalize(v1, v2, v3);
                    var n2 = MathUtils.FaceNormalize(v3, v4, v1);
                    _terrainNormal[i] = n1 + n2;
                }
            foreach (var v in _terrainNormal)
                v.Normalize();
        }

        private void CreateTerrainLight()
        {
            _backTerrainLight = new Color[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int i = y * Constants.TERRAIN_SIZE + x;
                    float lum = MathHelper.Clamp(Vector3.Dot(_terrainNormal[i], Light) + 0.5f, 0f, 1f);
                    _backTerrainLight[i] = _terrainLightData[i] * lum;
                }
        }

        private class WindCache
        {
            private readonly float[] _sinTable;
            private const int TableSize = 720;
            private const float TwoPi = (float)(Math.PI * 2);

            public WindCache()
            {
                _sinTable = new float[TableSize];
                for (int i = 0; i < TableSize; i++)
                    _sinTable[i] = (float)Math.Sin(i * TwoPi / TableSize);
            }

            public float FastSin(float x)
            {
                x %= TwoPi; if (x < 0) x += TwoPi;
                float idxF = x * TableSize / TwoPi;
                int idx = (int)idxF;
                float frac = idxF - idx;
                int next = (idx + 1) % TableSize;
                return _sinTable[idx] + (_sinTable[next] - _sinTable[idx]) * frac;
            }
        }

        private void InitTerrainWind(GameTime time)
        {
            if (_terrainGrassWind == null) return;
            double nowMs = time.TotalGameTime.TotalMilliseconds;
            if (nowMs - _lastUpdateTime < UpdateIntervalMs) return;

            float windSpeed = (float)(nowMs % 720_000 * 0.002);
            if (Math.Abs(windSpeed - _lastWindSpeed) < 0.01f) return;

            _lastWindSpeed = windSpeed;
            _lastUpdateTime = nowMs;

            var cam = Camera.Instance;
            int cx = (int)(cam.Position.X / Constants.TERRAIN_SCALE),
                cy = (int)(cam.Position.Y / Constants.TERRAIN_SCALE);
            int startX = Math.Max(0, cx - 32), startY = Math.Max(0, cy - 32);
            int endX = Math.Min(Constants.TERRAIN_SIZE - 1, startX + 64),
                endY = Math.Min(Constants.TERRAIN_SIZE - 1, startY + 64);
            const float Step = 5f;

            Parallel.For(startY, endY + 1, y =>
            {
                int baseIdx = y * Constants.TERRAIN_SIZE;
                for (int x = startX; x <= endX; x++)
                {
                    _terrainGrassWind[baseIdx + x] =
                        _windCache.FastSin(windSpeed + x * Step) * WindScale;
                }
            });
        }

        private void RenderTerrain(bool after)
        {
            if (_backTerrainHeight == null) return;
            if (!after) FrameMetrics.Reset();

            UpdateActiveLights();

            var effect = GraphicsManager.Instance.BasicEffect3D;
            effect.Projection = Camera.Instance.Projection;
            effect.View = Camera.Instance.View;

            foreach (var block in _visibleBlocks)
                if (block?.IsVisible == true)
                    RenderTerrainBlock(
                        block.Xi, block.Yi,
                        block.Xi, block.Yi,
                        after,
                        _lodSteps[block.LODLevel]);

            FlushAllTileBatches();
            FlushGrassBatch();
        }

        private void UpdateActiveLights()
        {
            _activeLights.Clear();
            if (_dynamicLights.Count == 0 || World == null) return;

            foreach (var light in _dynamicLights)
            {
                if (light.Intensity <= 0.001f) continue;
                var camPos = Camera.Instance.Position;
                var lightPos = new Vector2(light.Position.X, light.Position.Y);
                var cam2 = new Vector2(camPos.X, camPos.Y);
                if (Vector2.DistanceSquared(cam2, lightPos) > Constants.LOW_QUALITY_DISTANCE * Constants.LOW_QUALITY_DISTANCE)
                    continue;
                if (World.IsLightInView(light))
                    _activeLights.Add(light);
            }
        }

        private int GetLodLevel(float distance)
        {
            float f = distance / LodDistanceMultiplier;
            int l = (int)Math.Floor(f);
            float blend = f - l;
            l = (int)MathHelper.Lerp(l, l + 1, blend);
            return Math.Min(l, MaxLodLevels - 1);
        }

        private class TerrainBlock
        {
            public BoundingBox Bounds;
            public float MinZ, MaxZ;
            public int LODLevel;
            public Vector2 Center;
            public bool IsVisible;
            public int Xi, Yi;
        }

        private class TerrainBlockCache
        {
            private readonly TerrainBlock[,] _blocks;
            private readonly int _blockSize, _gridSize;

            public TerrainBlockCache(int blockSize, int terrainSize)
            {
                _blockSize = blockSize;
                _gridSize = terrainSize / blockSize;
                _blocks = new TerrainBlock[_gridSize, _gridSize];

                for (int y = 0; y < _gridSize; y++)
                    for (int x = 0; x < _gridSize; x++)
                        _blocks[y, x] = new TerrainBlock { Xi = x * blockSize, Yi = y * blockSize };
            }

            public TerrainBlock GetBlock(int x, int y) => _blocks[y, x];
        }

        private void PrecomputeBlockHeights()
        {
            if (_backTerrainHeight == null) return;
            int blocksPerSide = Constants.TERRAIN_SIZE / BlockSize;

            for (int by = 0; by < blocksPerSide; by++)
                for (int bx = 0; bx < blocksPerSide; bx++)
                {
                    var block = _blockCache.GetBlock(bx, by);
                    block.MinZ = float.MaxValue;
                    block.MaxZ = float.MinValue;

                    for (int y = 0; y < BlockSize; y++)
                        for (int x = 0; x < BlockSize; x++)
                        {
                            int idx = GetTerrainIndexRepeat(block.Xi + x, block.Yi + y);
                            float h = _backTerrainHeight[idx].B * 1.5f;
                            if (h < block.MinZ) block.MinZ = h;
                            if (h > block.MaxZ) block.MaxZ = h;
                        }

                    float sx = block.Xi * Constants.TERRAIN_SCALE,
                          sy = block.Yi * Constants.TERRAIN_SCALE,
                          ex = (block.Xi + BlockSize) * Constants.TERRAIN_SCALE,
                          ey = (block.Yi + BlockSize) * Constants.TERRAIN_SCALE;

                    block.Bounds = new BoundingBox(
                        new Vector3(sx, sy, block.MinZ),
                        new Vector3(ex, ey, block.MaxZ));
                }
        }

        private void UpdateVisibleBlocks(Vector2 camPos)
        {
            const float thrSq = CameraMoveThreshold * CameraMoveThreshold;
            if (Vector2.DistanceSquared(_lastCameraPosition, camPos) < thrSq)
                return;

            _lastCameraPosition = camPos;
            _visibleBlocks.Clear();

            float renderDist = Camera.Instance.ViewFar * 1.7f;
            float renderDistSq = renderDist * renderDist;
            int cellWorld = (int)(BlockSize * Constants.TERRAIN_SCALE);

            const int Extra = 4;
            int tilesPerAxis = Constants.TERRAIN_SIZE / BlockSize;

            int startX = Math.Max(0, (int)((camPos.X - renderDist) / cellWorld) - Extra),
                startY = Math.Max(0, (int)((camPos.Y - renderDist) / cellWorld) - Extra);
            int endX = Math.Min(tilesPerAxis - 1, (int)((camPos.X + renderDist) / cellWorld) + Extra),
                endY = Math.Min(tilesPerAxis - 1, (int)((camPos.Y + renderDist) / cellWorld) + Extra);

            var frustum = Camera.Instance.Frustum;
            var visible = new List<TerrainBlock>((endX - startX + 1) * (endY - startY + 1));

            for (int gy = startY; gy <= endY; gy++)
                for (int gx = startX; gx <= endX; gx++)
                {
                    var block = _blockCache.GetBlock(gx, gy);
                    block.Center = new Vector2(
                        (block.Xi + BlockSize * 0.5f) * Constants.TERRAIN_SCALE,
                        (block.Yi + BlockSize * 0.5f) * Constants.TERRAIN_SCALE);

                    float distSq = Vector2.DistanceSquared(block.Center, camPos);
                    if (distSq > renderDistSq)
                    {
                        block.IsVisible = false;
                        continue;
                    }

                    block.LODLevel = GetLodLevel(MathF.Sqrt(distSq));
                    block.IsVisible = frustum.Contains(block.Bounds) != ContainmentType.Disjoint;
                    if (block.IsVisible) visible.Add(block);
                }

            foreach (var block in visible)
                _visibleBlocks.Enqueue(block);
        }

        // Returns true if every tile in this patch has the same mapping (uniform)
        private bool IsPatchUniform(int xi, int yi, int size)
        {
            int idx0 = GetTerrainIndex(xi, yi);
            byte l1 = _mapping.Layer1[idx0],
                 l2 = _mapping.Layer2[idx0],
                 a0 = _mapping.Alpha[idx0];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int idx = GetTerrainIndex(xi + x, yi + y);
                    if (_mapping.Layer1[idx] != l1
                     || _mapping.Layer2[idx] != l2
                     || _mapping.Alpha[idx] != a0)
                        return false;
                }
            return true;
        }

        private void RenderTerrainBlock(
            float xf, float yf,
            int xi, int yi,
            bool after,
            int lodStep)
        {
            if (!after) FrameMetrics.DrawnBlocks++;

            // If higher LOD block is not uniform, force LOD=1
            if (lodStep > 1 && !IsPatchUniform(xi, yi, lodStep))
                lodStep = 1;

            GraphicsDevice.BlendState = BlendState.Opaque;

            for (int i = 0; i < BlockSize; i += lodStep)
                for (int j = 0; j < BlockSize; j += lodStep)
                    RenderTerrainTile(
                        xf + j, yf + i,
                        xi + j, yi + i,
                        (float)lodStep, lodStep,
                        after);
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

        /// <summary>
        /// Flushes buffered grass tufts and restores GPU state.
        /// </summary>
        private void FlushGrassBatch()
        {
            if (!Constants.DRAW_GRASS
                || _grassBatchCount == 0
                || _grassSpriteTexture == null)
                return;

            var dev = GraphicsDevice;
            var prevBlend = dev.BlendState;
            var prevDepth = dev.DepthStencilState;
            var prevRaster = dev.RasterizerState;
            var prevSampler = dev.SamplerStates[0];

            dev.BlendState = BlendState.AlphaBlend;
            dev.DepthStencilState = DepthStencilState.Default;
            dev.RasterizerState = RasterizerState.CullNone;
            dev.SamplerStates[0] = SamplerState.PointClamp;

            _grassEffect.World = Matrix.Identity;
            _grassEffect.View = Camera.Instance.View;
            _grassEffect.Projection = Camera.Instance.Projection;
            _grassEffect.Texture = _grassSpriteTexture;
            _grassEffect.AlphaFunction = CompareFunction.Greater;
            _grassEffect.ReferenceAlpha = 64;
            _grassEffect.VertexColorEnabled = true;

            int triCount = _grassBatchCount / 3;
            foreach (var pass in _grassEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                dev.DrawUserPrimitives(PrimitiveType.TriangleList, _grassBatch, 0, triCount);
                FrameMetrics.DrawCalls++;
                FrameMetrics.DrawnTriangles += triCount;
            }

            FrameMetrics.GrassFlushes++;
            _grassBatchCount = 0;

            dev.BlendState = prevBlend;
            dev.DepthStencilState = prevDepth;
            dev.RasterizerState = prevRaster;
            dev.SamplerStates[0] = prevSampler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GrassCount(float distSq)
        {
            if (distSq < GrassNearSq) return 6;
            if (distSq < GrassMidSq) return 2;
            if (distSq < GrassFarSq) return 1;
            return 0;
        }

        private void RenderTerrainTile(
            float xf, float yf,
            int xi, int yi,
            float lodFactor, int lodInt,
            bool after)
        {
            if (after || _terrain == null) return;

            int i1 = GetTerrainIndex(xi, yi);
            if (_terrain.TerrainWall[i1].HasFlag(TWFlags.NoGround)) return;

            int i2 = GetTerrainIndex(xi + lodInt, yi);
            int i3 = GetTerrainIndex(xi + lodInt, yi + lodInt);
            int i4 = GetTerrainIndex(xi, yi + lodInt);

            PrepareTileVertices(xi, yi, xf, yf, i1, i2, i3, i4, lodFactor);
            PrepareTileLights(i1, i2, i3, i4);

            byte a1 = i1 < _mapping.Alpha.Length ? _mapping.Alpha[i1] : (byte)0;
            byte a2 = i2 < _mapping.Alpha.Length ? _mapping.Alpha[i2] : (byte)0;
            byte a3 = i3 < _mapping.Alpha.Length ? _mapping.Alpha[i3] : (byte)0;
            byte a4 = i4 < _mapping.Alpha.Length ? _mapping.Alpha[i4] : (byte)0;

            bool isOpaque = (a1 & a2 & a3 & a4) == 255;
            bool hasAlpha = (a1 | a2 | a3 | a4) != 0;

            if (isOpaque)
            {
                RenderTexture(_mapping.Layer2[i1], xf, yf, lodFactor, useBatch: true, alphaLayer: false);
            }
            else
            {
                RenderTexture(_mapping.Layer1[i1], xf, yf, lodFactor, useBatch: true, alphaLayer: false);
                if (hasAlpha)
                {
                    ApplyAlphaToLights(a1, a2, a3, a4);
                    RenderTexture(_mapping.Layer2[i1], xf, yf, lodFactor, useBatch: true, alphaLayer: true);
                }
            }

            // Draw grass if enabled and this tile uses a registered grass texture            byte baseTex = isOpaque ? _mapping.Layer2[i1] : _mapping.Layer1[i1];
            byte baseTex = isOpaque ? _mapping.Layer2[i1] : _mapping.Layer1[i1];
            if (!Constants.DRAW_GRASS
                || WorldIndex == 11
                || _grassSpriteTexture == null
                || !GrassTextureIndices.Contains(baseTex))
                return;

            var camPos = Camera.Instance.Position;
            float tileCx = (xf + 0.5f * lodFactor) * Constants.TERRAIN_SCALE;
            float tileCy = (yf + 0.5f * lodFactor) * Constants.TERRAIN_SCALE;
            float dx = camPos.X - tileCx, dy = camPos.Y - tileCy;
            float distSq = dx * dx + dy * dy;

            int grassPerTile = GrassCount(distSq);
            if (grassPerTile == 0) return;

            var tileLight = i1 < _backTerrainLight.Length
                          ? _backTerrainLight[i1]
                          : Color.White;
            float windBase = GetWindValue(xi, yi);

            const float GrassUWidth = 0.30f;
            const float ScaleMin = 1.0f;
            const float ScaleMax = 3.0f;
            const float RotJitterDeg = 90f;
            const float HeightOffset = 55f;

            for (int i = 0; i < grassPerTile; i++)
            {
                float u0 = PseudoRandom(xi, yi, 123 + i) * (1f - GrassUWidth);
                float u1 = u0 + GrassUWidth;
                float halfUV = GrassUWidth * 0.5f;
                float maxOffset = 0.5f - halfUV;

                float rx = (PseudoRandom(xi, yi, 17 + i) * 2f - 1f) * maxOffset;
                float ry = (PseudoRandom(xi, yi, 91 + i) * 2f - 1f) * maxOffset;

                float worldX = (xf + 0.5f * lodFactor + rx * lodFactor) * Constants.TERRAIN_SCALE;
                float worldY = (yf + 0.5f * lodFactor + ry * lodFactor) * Constants.TERRAIN_SCALE;
                float h = RequestTerrainHeight(worldX, worldY);

                float scale = MathHelper.Lerp(ScaleMin, ScaleMax, PseudoRandom(xi, yi, 33 + i));
                float jitter = MathHelper.ToRadians((PseudoRandom(xi, yi, 57 + i) - 0.5f) * 2f * RotJitterDeg);
                float windZ = MathHelper.ToRadians(windBase * 0.05f) + jitter;

                RenderGrassQuad(
                    new Vector3(worldX, worldY, h + HeightOffset),
                    lodFactor * scale,
                    windZ,
                    tileLight,
                    u0, u1
                );
            }
        }

        /// <summary>
        /// Renders a single tile texture, either batched or immediately (for alpha layers).
        /// </summary>
        private void RenderTexture(
            int textureIndex,
            float xf, float yf,
            float lodScale,
            bool useBatch,
            bool alphaLayer = false)
        {
            if (Status != Models.GameControlStatus.Ready
                || textureIndex < 0
                || textureIndex >= _textures.Length
                || _textures[textureIndex] == null)
                return;

            var texture = _textures[textureIndex];

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

                _terrainTextureCoords[0].X = suf + flowOff.X
                    + (float)Math.Sin((suf + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[0].Y = svf + flowOff.Y
                    + (float)Math.Cos((svf + waterPhase) * DistortionFrequency) * DistortionAmplitude;

                _terrainTextureCoords[1].X = suf + uvW + flowOff.X
                    + (float)Math.Sin((suf + uvW + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoords[1].Y = svf + flowOff.Y
                    + (float)Math.Cos((svf + waterPhase) * DistortionFrequency) * DistortionAmplitude;

                _terrainTextureCoords[2].X = _terrainTextureCoords[1].X;
                _terrainTextureCoords[2].Y = svf + uvH + flowOff.Y
                    + (float)Math.Cos((svf + uvH + waterPhase) * DistortionFrequency) * DistortionAmplitude;

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

            // Build six vertices for the quad
            _terrainVertices[0] = new VertexPositionColorTexture(_tempTerrainVertex[0], _tempTerrainLights[0], _terrainTextureCoords[0]);
            _terrainVertices[1] = new VertexPositionColorTexture(_tempTerrainVertex[1], _tempTerrainLights[1], _terrainTextureCoords[1]);
            _terrainVertices[2] = new VertexPositionColorTexture(_tempTerrainVertex[2], _tempTerrainLights[2], _terrainTextureCoords[2]);
            _terrainVertices[3] = new VertexPositionColorTexture(_tempTerrainVertex[2], _tempTerrainLights[2], _terrainTextureCoords[2]);
            _terrainVertices[4] = new VertexPositionColorTexture(_tempTerrainVertex[3], _tempTerrainLights[3], _terrainTextureCoords[3]);
            _terrainVertices[5] = new VertexPositionColorTexture(_tempTerrainVertex[0], _tempTerrainLights[0], _terrainTextureCoords[0]);

            if (useBatch)
            {
                AddTileToBatch(textureIndex, _terrainVertices, alphaLayer);
                return;
            }

            // Immediate draw (for alpha layers)
            var basicEffect = GraphicsManager.Instance.BasicEffect3D;
            basicEffect.Texture = texture;
            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, 2);
            }
            FrameMetrics.DrawCalls++;
            FrameMetrics.DrawnTriangles += 2;
        }

        private void RenderGrassQuad(
            Vector3 position,
            float lodFactor,
            float windRotationZ,
            Color lightColor,
            float u0,
            float u1)
        {
            const float BaseW = 130f, BaseH = 30f;
            float w = BaseW * (u1 - u0) * lodFactor;
            float h = BaseH * lodFactor;
            float hw = w * 0.5f;

            // Local quad corners
            var p1 = new Vector3(-hw, 0, 0);
            var p2 = new Vector3(hw, 0, 0);
            var p3 = new Vector3(-hw, 0, h);
            var p4 = new Vector3(hw, 0, h);

            var t1 = new Vector2(u0, 1);
            var t2 = new Vector2(u1, 1);
            var t3 = new Vector2(u0, 0);
            var t4 = new Vector2(u1, 0);

            var world = Matrix.CreateRotationZ(MathHelper.ToRadians(45f) + windRotationZ)
                      * Matrix.CreateTranslation(position);

            var wp1 = Vector3.Transform(p1, world);
            var wp2 = Vector3.Transform(p2, world);
            var wp3 = Vector3.Transform(p3, world);
            var wp4 = Vector3.Transform(p4, world);

            var finalColor = new Color(
                (byte)Math.Min(lightColor.R * GrassBrightness, 255f),
                (byte)Math.Min(lightColor.G * GrassBrightness, 255f),
                (byte)Math.Min(lightColor.B * GrassBrightness, 255f)
            );

            // Flush if batch is full
            if (_grassBatchCount + 6 >= GrassBatchVerts)
                FlushGrassBatch();

            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp1, finalColor, t1);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp2, finalColor, t2);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp3, finalColor, t3);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp2, finalColor, t2);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp4, finalColor, t4);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp3, finalColor, t3);
        }

        // 32-bit Xorshift* hash → float [0..1]
        private static float PseudoRandom(int x, int y, int salt = 0)
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
            h ^= h >> 13; h *= 0x165667B1u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }

        private void PrepareTileVertices(
            int xi, int yi,
            float xf, float yf,
            int i1, int i2, int i3, int i4,
            float lodFactor)
        {
            float h1 = i1 < _backTerrainHeight.Length ? _backTerrainHeight[i1].B * 1.5f : 0f;
            float h2 = i2 < _backTerrainHeight.Length ? _backTerrainHeight[i2].B * 1.5f : 0f;
            float h3 = i3 < _backTerrainHeight.Length ? _backTerrainHeight[i3].B * 1.5f : 0f;
            float h4 = i4 < _backTerrainHeight.Length ? _backTerrainHeight[i4].B * 1.5f : 0f;

            float sx = xf * Constants.TERRAIN_SCALE;
            float sy = yf * Constants.TERRAIN_SCALE;
            float ss = Constants.TERRAIN_SCALE * lodFactor;

            _tempTerrainVertex[0] = new Vector3(sx, sy, h1);
            _tempTerrainVertex[1] = new Vector3(sx + ss, sy, h2);
            _tempTerrainVertex[2] = new Vector3(sx + ss, sy + ss, h3);
            _tempTerrainVertex[3] = new Vector3(sx, sy + ss, h4);

            // Add special height if flagged
            if (i1 < _terrain.TerrainWall.Length && _terrain.TerrainWall[i1].HasFlag(TWFlags.Height))
                _tempTerrainVertex[0].Z += SpecialHeight;
            if (i2 < _terrain.TerrainWall.Length && _terrain.TerrainWall[i2].HasFlag(TWFlags.Height))
                _tempTerrainVertex[1].Z += SpecialHeight;
            if (i3 < _terrain.TerrainWall.Length && _terrain.TerrainWall[i3].HasFlag(TWFlags.Height))
                _tempTerrainVertex[2].Z += SpecialHeight;
            if (i4 < _terrain.TerrainWall.Length && _terrain.TerrainWall[i4].HasFlag(TWFlags.Height))
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
            Vector3 baseColor = index < _backTerrainLight.Length
                ? new Vector3(_backTerrainLight[index].R, _backTerrainLight[index].G, _backTerrainLight[index].B)
                : Vector3.Zero;

            baseColor += new Vector3(AmbientLight * 255f);
            baseColor += EvaluateDynamicLight(new Vector2(pos.X, pos.Y));
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
            effect.Texture = _textures[texIndex];
            GraphicsDevice.BlendState = alphaLayer ? BlendState.AlphaBlend : BlendState.Opaque;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    batch, 0,
                    vertCount / 3);
            }

            FrameMetrics.DrawCalls += 1;
            FrameMetrics.DrawnTriangles += vertCount / 3;

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
            GraphicsDevice.BlendState = BlendState.Opaque;
        }
    }
}
