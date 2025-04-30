using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Client.Main.Utils;

namespace Client.Main.Controls
{
    public class TerrainControl : GameControl
    {
        // Constants
        private const float SpecialHeight = 1200f;
        private const int BlockSize = 4;
        private const int MAX_LOD_LEVELS = 2;
        private const float LOD_DISTANCE_MULTIPLIER = 3000f;
        private const float WindScale = 10f;
        private const int UPDATE_INTERVAL_MS = 32;
        private const float CAMERA_MOVE_THRESHOLD = 32f;
        private const byte BASE_GRASS_TEXTURE_INDEX = 0;

        // Grass Constants
        private const float GRASS_NEAR = 2500f;
        private const float GRASS_FAR = 3300f;
        private const float GRASS_FAR_SQ = GRASS_FAR * GRASS_FAR;
        private const int GRASS_BATCH_QUADS = 4096;          // 4096 tufts per batch
        private const int GRASS_BATCH_VERTS = GRASS_BATCH_QUADS * 6;

        // Public Properties
        public short WorldIndex { get; set; }
        public Vector3 Light { get; set; } = new Vector3(0.5f, -0.5f, 0.5f);
        public Dictionary<int, string> TextureMappingFiles { get; set; } = new Dictionary<int, string>
        {
            { 0, "TileGrass01.ozj" },
            { 1, "TileGrass02.ozj" },
            { 2, "TileGround01.ozj" },
            { 3, "TileGround02.ozj" },
            { 4, "TileGround03.ozj" },
            { 5, "TileWater01.ozj" },
            { 6, "TileWood01.ozj" },
            { 7, "TileRock01.ozj" },
            { 8, "TileRock02.ozj" },
            { 9, "TileRock03.ozj" },
            { 10, "TileRock04.ozj" },
            { 11, "TileRock05.ozj" },
            { 12, "TileRock06.ozj" },
            { 13, "TileRock07.ozj" },
            { 30, "TileGrass01.ozt" },
            { 31, "TileGrass02.ozt" },
            { 32, "TileGrass03.ozt" },
            { 100, "leaf01.ozt" },
            { 101, "leaf02.ozj" },
            { 102, "rain01.ozt" },
            { 103, "rain02.ozt" },
            { 104, "rain03.ozt" }
        };
        public float WaterSpeed { get; set; } = 0f; // Speed factor for water animation
        public float DistortionAmplitude { get; set; } = 0f; // Amplitude of UV distortion for water effect
        public float DistortionFrequency { get; set; } = 0f; // Frequency of UV distortion for water effect
        public float GrassBrightness { get; set; } = 2f;

        // Private Fields
        private TerrainAttribute _terrain;
        private TerrainMapping _mapping;
        private Texture2D[] _textures;
        private float[] _terrainGrassWind;
        private Color[] _backTerrainLight;
        private Vector3[] _terrainNormal;
        private Color[] _backTerrainHeight;
        private Color[] _terrainLightData;

        private Vector2 _waterFlowDir = Vector2.UnitX;
        private float waterTotal = 0f; // Continuous accumulator for water texture offset (do not wrap it)

        // Grass Resources
        private Texture2D _grassSpriteTexture; // TileGrass01.ozt/tga texture
        private AlphaTestEffect _grassEffect; // Effect for drawing grass

        // Terrain Rendering Data
        private readonly VertexPositionColorTexture[] _terrainVertices = new VertexPositionColorTexture[6];
        private readonly Vector2[] _terrainTextureCoord;
        private readonly Vector3[] _tempTerrainVertex;
        private readonly Color[] _tempTerrainLights;

        // Wind Data
        private float _lastWindSpeed = float.MinValue;
        private double _lastUpdateTime;
        private readonly WindCache _windCache = new WindCache();

        // LOD and Culling Data
        private readonly int[] LOD_STEPS = { 1, 4 };
        private readonly TerrainBlockCache _blockCache;
        private readonly Queue<TerrainBlock> _visibleBlocks = new Queue<TerrainBlock>(64);
        private Vector2 _lastCameraPosition;

        // Grass Batching Data
        private readonly VertexPositionColorTexture[] _grassBatch =
                           new VertexPositionColorTexture[GRASS_BATCH_VERTS];
        private int _grassBatchCount = 0;

        // Water Flow Direction Property
        public Vector2 WaterFlowDirection
        {
            get => _waterFlowDir;
            set => _waterFlowDir = value.LengthSquared() < 1e-4f
                                   ? Vector2.UnitX
                                   : Vector2.Normalize(value);
        }

        public TerrainControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
            _blockCache = new TerrainBlockCache(BlockSize, Constants.TERRAIN_SIZE);
            _terrainTextureCoord = new Vector2[4];
            _tempTerrainVertex = new Vector3[4];
            _tempTerrainLights = new Color[4];
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

            string attPath = GetActualPath(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att"));
            if (!string.IsNullOrEmpty(attPath))
            {
                tasks.Add(terrainReader.Load(attPath).ContinueWith(t => _terrain = t.Result));
            }
            string heightPath = GetActualPath(Path.Combine(fullPathWorldFolder, "TerrainHeight.OZB"));
            if (!string.IsNullOrEmpty(heightPath))
            {
                tasks.Add(ozbReader.Load(heightPath)
                    .ContinueWith(t => _backTerrainHeight = t.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            }
            string mapPath = GetActualPath(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map"));
            if (!string.IsNullOrEmpty(mapPath))
            {
                tasks.Add(mappingReader.Load(mapPath).ContinueWith(t => _mapping = t.Result));
            }

            var textureMapFiles = new string[256];

            foreach (var kvp in TextureMappingFiles)
            {
                string texPath = GetActualPath(Path.Combine(fullPathWorldFolder, kvp.Value));
                textureMapFiles[kvp.Key] = texPath;
            }

            for (int i = 1; i <= 16; i++)
            {
                string extTilePath = GetActualPath(Path.Combine(fullPathWorldFolder, $"ExtTile{i:00}.ozj"));
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

            string grassSpritePath = Path.Combine(worldFolder, "TileGrass01.ozt");
            try
            {
                _grassSpriteTexture = await TextureLoader.Instance.PrepareAndGetTexture(grassSpritePath);
                if (_grassSpriteTexture != null)
                {
                    PremultiplyAlpha(_grassSpriteTexture);
                }
                if (_grassSpriteTexture == null)
                {
                    Console.WriteLine($"Warning: Could not load grass sprite texture: {grassSpritePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading grass sprite texture '{grassSpritePath}': {ex.Message}");
            }

            _grassEffect = GraphicsManager.Instance.AlphaTestEffect3D;

            await base.Load();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (Status != Models.GameControlStatus.Ready)
                return;

            InitTerrainWind(time);

            // Increase continuous water offset using WaterSpeed property.
            // The waterTotal is not wrapped here; the sampler with LinearWrap takes care of base UV wrapping.
            waterTotal += (float)time.ElapsedGameTime.TotalSeconds * WaterSpeed;
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

        public TWFlags RequestTerrainFlag(int x, int y) => _terrain.TerrainWall[GetTerrainIndex(x, y)];

        public float RequestTerrainHeight(float xf, float yf)
        {
            if (_terrain?.TerrainWall == null || xf < 0.0f || yf < 0.0f || _backTerrainHeight == null || float.IsNaN(xf) || float.IsNaN(yf))
                return 0.0f;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf;
            int yi = (int)yf;
            int index = GetTerrainIndex(xi, yi);

            if (index >= _backTerrainHeight.Length || (_terrain.TerrainWall.Length > index && _terrain.TerrainWall[index].HasFlag(TWFlags.Height)))
                return SpecialHeight;

            float xd = xf - xi;
            float yd = yf - yi;

            // Pre-check the bounds before getting indices
            int x1 = xi & Constants.TERRAIN_SIZE_MASK;
            int y1 = yi & Constants.TERRAIN_SIZE_MASK;
            int x2 = (xi + 1) & Constants.TERRAIN_SIZE_MASK;
            int y2 = (yi + 1) & Constants.TERRAIN_SIZE_MASK;

            int index1 = y1 * Constants.TERRAIN_SIZE + x1;
            int index2 = y1 * Constants.TERRAIN_SIZE + x2;
            int index3 = y2 * Constants.TERRAIN_SIZE + x2;
            int index4 = y2 * Constants.TERRAIN_SIZE + x1;

            float h1 = _backTerrainHeight[index1].B;
            float h2 = _backTerrainHeight[index2].B;
            float h3 = _backTerrainHeight[index3].B;
            float h4 = _backTerrainHeight[index4].B;

            // Bilinear interpolation in one go
            return (1 - xd) * (1 - yd) * h1 +
                   xd * (1 - yd) * h2 +
                   xd * yd * h3 +
                   (1 - xd) * yd * h4;
        }

        public Vector3 RequestTerrainLight(float xf, float yf)
        {
            if (_terrain?.TerrainWall == null || xf < 0.0f || yf < 0.0f || _backTerrainLight == null)
                return Vector3.One;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf;
            int yi = (int)yf;
            float xd = xf - xi;
            float yd = yf - yi;

            int index1 = xi + yi * Constants.TERRAIN_SIZE;
            int index2 = (xi + 1) + yi * Constants.TERRAIN_SIZE;
            int index3 = (xi + 1) + (yi + 1) * Constants.TERRAIN_SIZE;
            int index4 = xi + (yi + 1) * Constants.TERRAIN_SIZE;

            if (new[] { index1, index2, index3, index4 }.Any(i => i < 0 || i >= _backTerrainLight.Length))
                return Vector3.Zero;

            float[] output = new float[3];

            for (int i = 0; i < 3; i++)
            {
                float left = 0f, right = 0f;

                if (_backTerrainLight != null)
                {
                    switch (i)
                    {
                        case 0:
                            left = MathHelper.Lerp(_backTerrainLight[index1].R, _backTerrainLight[index4].R, yd);
                            right = MathHelper.Lerp(_backTerrainLight[index2].R, _backTerrainLight[index3].R, yd);
                            break;
                        case 1:
                            left = MathHelper.Lerp(_backTerrainLight[index1].G, _backTerrainLight[index4].G, yd);
                            right = MathHelper.Lerp(_backTerrainLight[index2].G, _backTerrainLight[index3].G, yd);
                            break;
                        case 2:
                            left = MathHelper.Lerp(_backTerrainLight[index1].B, _backTerrainLight[index4].B, yd);
                            right = MathHelper.Lerp(_backTerrainLight[index2].B, _backTerrainLight[index3].B, yd);
                            break;
                    }
                }

                output[i] = MathHelper.Lerp(left, right, xd);
            }

            return new Vector3(output[0], output[1], output[2]);
        }

        public float GetWindValue(int x, int y)
        {
            int index = y * Constants.TERRAIN_SIZE + x;
            return _terrainGrassWind[index];
        }

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

        private static int GetTerrainIndex(int x, int y) => y * Constants.TERRAIN_SIZE + x;

        private static int GetTerrainIndexRepeat(int x, int y) =>
            ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE) + (x & Constants.TERRAIN_SIZE_MASK);

        private void CreateTerrainNormal()
        {
            _terrainNormal = new Vector3[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int index = GetTerrainIndex(x, y);

                    Vector3 v1 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y)].B);
                    Vector3 v2 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y + 1)].B);
                    Vector3 v3 = new Vector3(x * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x, y + 1)].B);
                    Vector3 v4 = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x, y)].B);

                    Vector3 faceNormal1 = MathUtils.FaceNormalize(v1, v2, v3);
                    Vector3 faceNormal2 = MathUtils.FaceNormalize(v3, v4, v1);

                    _terrainNormal[index] += faceNormal1 + faceNormal2;
                }
            }

            for (int i = 0; i < _terrainNormal.Length; i++)
            {
                _terrainNormal[i].Normalize();
            }
        }

        private void CreateTerrainLight()
        {
            _backTerrainLight = new Color[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int index = GetTerrainIndex(x, y);
                    float luminosity = MathHelper.Clamp(Vector3.Dot(_terrainNormal[index], Light) + 0.5f, 0f, 1f);
                    _backTerrainLight[index] = _terrainLightData[index] * luminosity;
                }
            }
        }

        private class WindCache
        {
            private readonly float[] _sinLookupTable;
            private const int TABLE_SIZE = 720;
            private const float TWO_PI = (float)(Math.PI * 2);

            public WindCache()
            {
                _sinLookupTable = new float[TABLE_SIZE];
                for (int i = 0; i < TABLE_SIZE; i++)
                {
                    float angle = (i * TWO_PI) / TABLE_SIZE;
                    _sinLookupTable[i] = (float)Math.Sin(angle);
                }
            }

            public float FastSin(float x)
            {
                x = x % TWO_PI;
                if (x < 0) x += TWO_PI;

                float indexF = x * TABLE_SIZE / TWO_PI;
                int index = (int)indexF;

                float fraction = indexF - index;
                int nextIndex = (index + 1) % TABLE_SIZE;

                return _sinLookupTable[index] + (_sinLookupTable[nextIndex] - _sinLookupTable[index]) * fraction;
            }
        }

        private void InitTerrainWind(GameTime time)
        {
            if (_terrainGrassWind == null) return;

            if (time.TotalGameTime.TotalMilliseconds - _lastUpdateTime < UPDATE_INTERVAL_MS)
                return;

            float windSpeed = (float)(time.TotalGameTime.TotalMilliseconds % 720_000 * 0.002);

            if (Math.Abs(windSpeed - _lastWindSpeed) < 0.01f)
                return;

            _lastWindSpeed = windSpeed;
            _lastUpdateTime = time.TotalGameTime.TotalMilliseconds;

            var cam = Camera.Instance;
            int startX = Math.Max(0, (int)(cam.Position.X / Constants.TERRAIN_SCALE) - 32);
            int startY = Math.Max(0, (int)(cam.Position.Y / Constants.TERRAIN_SCALE) - 32);
            int endX = Math.Min(Constants.TERRAIN_SIZE - 1, startX + 64);
            int endY = Math.Min(Constants.TERRAIN_SIZE - 1, startY + 64);

            const float STEP = 5f;

            Parallel.For(startY, endY + 1, y =>
            {
                int baseIdx = y * Constants.TERRAIN_SIZE;
                for (int x = startX; x <= endX; x++)
                {
                    int idx = baseIdx + x;
                    _terrainGrassWind[idx] =
                        _windCache.FastSin(windSpeed + x * STEP) * WindScale;
                }
            });
        }


        private void RenderTerrain(bool isAfter)
        {
            if (_backTerrainHeight == null) return;

            UpdateVisibleBlocks(new Vector2(Camera.Instance.Position.X,
                                            Camera.Instance.Position.Y));

            var effect = GraphicsManager.Instance.BasicEffect3D;
            effect.Projection = Camera.Instance.Projection;
            effect.View = Camera.Instance.View;

            foreach (var block in _visibleBlocks)
            {
                if (block == null || !block.IsVisible) continue;

                RenderTerrainBlock(
                    block.Xi, block.Yi,
                    block.Xi, block.Yi,
                    isAfter,
                    LOD_STEPS[block.LODLevel]);
            }

            FlushGrassBatch();
        }

        private int GetLODLevel(float distance)
        {
            float levelF = distance / LOD_DISTANCE_MULTIPLIER;
            int level = (int)Math.Floor(levelF);

            float blend = levelF - level;
            level = (int)MathHelper.Lerp(level, level + 1, blend);

            return Math.Min(level, MAX_LOD_LEVELS - 1);
        }

        private class TerrainBlock
        {
            public BoundingBox Bounds;
            public float MinZ;
            public float MaxZ;
            public int LODLevel;
            public Vector2 Center;
            public bool IsVisible;
            public int Xi;
            public int Yi;
        }

        private class TerrainBlockCache
        {
            private readonly TerrainBlock[,] _blocks;
            private readonly int _blockSize;
            private readonly int _gridSize;

            public TerrainBlockCache(int blockSize, int terrainSize)
            {
                _blockSize = blockSize;
                _gridSize = terrainSize / blockSize;
                _blocks = new TerrainBlock[_gridSize, _gridSize];

                for (int y = 0; y < _gridSize; y++)
                {
                    for (int x = 0; x < _gridSize; x++)
                    {
                        _blocks[y, x] = new TerrainBlock
                        {
                            Xi = x * blockSize,
                            Yi = y * blockSize
                        };
                    }
                }
            }

            public TerrainBlock GetBlock(int x, int y) => _blocks[y, x];
        }

        private void PrecomputeBlockHeights()
        {
            if (_backTerrainHeight == null) return;

            for (int gy = 0; gy < Constants.TERRAIN_SIZE / BlockSize; gy++)
            {
                for (int gx = 0; gx < Constants.TERRAIN_SIZE / BlockSize; gx++)
                {
                    TerrainBlock block = _blockCache.GetBlock(gx, gy);

                    float minZ = float.MaxValue;
                    float maxZ = float.MinValue;

                    for (int y = 0; y < BlockSize; y++)
                        for (int x = 0; x < BlockSize; x++)
                        {
                            int idx = GetTerrainIndexRepeat(block.Xi + x, block.Yi + y);
                            float h = _backTerrainHeight[idx].B * 1.5f;
                            if (h < minZ) minZ = h;
                            if (h > maxZ) maxZ = h;
                        }

                    block.MinZ = minZ;
                    block.MaxZ = maxZ;

                    float sx = block.Xi * Constants.TERRAIN_SCALE;
                    float sy = block.Yi * Constants.TERRAIN_SCALE;
                    float ex = (block.Xi + BlockSize) * Constants.TERRAIN_SCALE;
                    float ey = (block.Yi + BlockSize) * Constants.TERRAIN_SCALE;

                    block.Bounds = new BoundingBox(
                        new Vector3(sx, sy, minZ),
                        new Vector3(ex, ey, maxZ));
                }
            }
        }

        private void UpdateVisibleBlocks(Vector2 cameraPos)
        {
            const float THRESHOLD_SQ = CAMERA_MOVE_THRESHOLD * CAMERA_MOVE_THRESHOLD;
            if (Vector2.DistanceSquared(_lastCameraPosition, cameraPos) < THRESHOLD_SQ)
                return;

            _lastCameraPosition = cameraPos;
            _visibleBlocks.Clear();

            float renderDist = Camera.Instance.ViewFar * 1.7f;
            float renderDistSq = renderDist * renderDist;
            int cellWorld = (int)(Constants.TERRAIN_SCALE * BlockSize);

            const int EXTRA = 4;

            int tilesPerAxis = Constants.TERRAIN_SIZE / BlockSize;

            int startX = Math.Max(0, (int)((cameraPos.X - renderDist) / cellWorld) - EXTRA);
            int startY = Math.Max(0, (int)((cameraPos.Y - renderDist) / cellWorld) - EXTRA);
            int endX = Math.Min(tilesPerAxis - 1, (int)((cameraPos.X + renderDist) / cellWorld) + EXTRA);
            int endY = Math.Min(tilesPerAxis - 1, (int)((cameraPos.Y + renderDist) / cellWorld) + EXTRA);

            var frustum = Camera.Instance.Frustum;
            var visible = new List<TerrainBlock>((endX - startX + 1) * (endY - startY + 1));

            for (int gy = startY; gy <= endY; gy++)
                for (int gx = startX; gx <= endX; gx++)
                {
                    TerrainBlock block = _blockCache.GetBlock(gx, gy);

                    block.Center = new Vector2(
                        (block.Xi + BlockSize * 0.5f) * Constants.TERRAIN_SCALE,
                        (block.Yi + BlockSize * 0.5f) * Constants.TERRAIN_SCALE);

                    float distSq = Vector2.DistanceSquared(block.Center, cameraPos);
                    if (distSq > renderDistSq)
                    {
                        block.IsVisible = false;
                        continue;
                    }

                    block.LODLevel = GetLODLevel(MathF.Sqrt(distSq));
                    block.IsVisible = frustum.Contains(block.Bounds) != ContainmentType.Disjoint;

                    if (block.IsVisible)
                        visible.Add(block);
                }

            foreach (var block in visible)
                _visibleBlocks.Enqueue(block);
        }

        private void RenderTerrainBlock(float xf, float yf, int xi, int yi, bool isAfter, int lodStep)
        {
            if (BlockSize % lodStep != 0)
            {
                lodStep = 1;
            }

            GraphicsDevice.BlendState = BlendState.Opaque;

            // Batch terrain tiles for rendering
            for (int i = 0; i < BlockSize; i += lodStep)
            {
                for (int j = 0; j < BlockSize; j += lodStep)
                {
                    RenderTerrainTile(xf + j, yf + i, xi + j, yi + i, (float)lodStep, lodStep, isAfter);
                }
            }
        }


        private static void PremultiplyAlpha(Texture2D tex)
        {
            if (tex.Format != SurfaceFormat.Color || tex.IsDisposed)
                return;

            int len = tex.Width * tex.Height;
            Color[] px = new Color[len];
            tex.GetData(px);

            for (int i = 0; i < len; i++)
            {
                byte a = px[i].A;
                if (a == 255) continue;                // full alpha - no change
                px[i] = new Color(
                    (byte)(px[i].R * a / 255),
                    (byte)(px[i].G * a / 255),
                    (byte)(px[i].B * a / 255),
                    a);
            }

            tex.SetData(px);
        }

        /// <summary>Renders the buffered grass tufts and restores GPU states.</summary>
        private void FlushGrassBatch()
        {
            if (_grassBatchCount == 0 || _grassSpriteTexture == null)
                return;

            //--------------------------------------------------------------------
            // 1)  SAVE STATES that are changed during grass drawing
            //--------------------------------------------------------------------
            var dev = GraphicsDevice;
            var prevBlend = dev.BlendState;
            var prevDepth = dev.DepthStencilState;
            var prevRaster = dev.RasterizerState;
            var prevSampler0 = dev.SamplerStates[0];

            //--------------------------------------------------------------------
            // 2)  SET STATES for grass
            //--------------------------------------------------------------------
            dev.BlendState = BlendState.AlphaBlend;          // pre-multiplied alpha
            dev.DepthStencilState = DepthStencilState.Default;
            dev.RasterizerState = RasterizerState.CullNone;       // no culling for quads
            dev.SamplerStates[0] = SamplerState.PointClamp;        // atlas + sharp edges

            //--------------------------------------------------------------------
            // 3)  CONFIGURE EFFECT AND DRAW BUFFER
            //--------------------------------------------------------------------
            _grassEffect.World = Matrix.Identity;
            _grassEffect.View = Camera.Instance.View;
            _grassEffect.Projection = Camera.Instance.Projection;
            _grassEffect.Texture = _grassSpriteTexture;
            _grassEffect.AlphaFunction = CompareFunction.Greater;
            _grassEffect.ReferenceAlpha = 64;
            _grassEffect.VertexColorEnabled = true;

            foreach (var pass in _grassEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                dev.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _grassBatch, 0,
                    _grassBatchCount / 3);      // 3 vertices = 1 triangle
            }

            _grassBatchCount = 0;               // buffer ready for the next frame

            //--------------------------------------------------------------------
            // 4)  RESTORE PREVIOUS STATES (terrain will return to LinearWrap)
            //--------------------------------------------------------------------
            dev.BlendState = prevBlend;
            dev.DepthStencilState = prevDepth;
            dev.RasterizerState = prevRaster;
            dev.SamplerStates[0] = prevSampler0;
        }

        private void RenderTerrainTile(
            float xf, float yf,
            int xi, int yi,
            float lodf, int lodi,
            bool isAfter)
        {
            if (isAfter || _terrain == null)
                return;

            int idx1 = GetTerrainIndex(xi, yi);
            if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.NoGround))
                return;

            int idx2 = GetTerrainIndex(xi + lodi, yi);
            int idx3 = GetTerrainIndex(xi + lodi, yi + lodi);
            int idx4 = GetTerrainIndex(xi, yi + lodi);

            PrepareTileVertices(xi, yi, xf, yf, idx1, idx2, idx3, idx4, lodf);
            PrepareTileLights(idx1, idx2, idx3, idx4);

            byte a1 = idx1 < _mapping.Alpha.Length ? _mapping.Alpha[idx1] : (byte)0;
            byte a2 = idx2 < _mapping.Alpha.Length ? _mapping.Alpha[idx2] : (byte)0;
            byte a3 = idx3 < _mapping.Alpha.Length ? _mapping.Alpha[idx3] : (byte)0;
            byte a4 = idx4 < _mapping.Alpha.Length ? _mapping.Alpha[idx4] : (byte)0;

            bool isOpaque = a1 == 255 && a2 == 255 && a3 == 255 && a4 == 255;
            bool hasAlpha = a1 > 0 || a2 > 0 || a3 > 0 || a4 > 0;

            float lodScale = lodf;

            if (isOpaque)
                RenderTexture(_mapping.Layer2[idx1], xf, yf, lodScale);
            else
                RenderTexture(_mapping.Layer1[idx1], xf, yf, lodScale);

            if (hasAlpha && !isOpaque)
            {
                ApplyAlphaToLights(a1, a2, a3, a4);
                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                RenderTexture(_mapping.Layer2[idx1], xf, yf, lodScale);
            }

            byte baseTex = (a1 < 255) ? _mapping.Layer1[idx1] : _mapping.Layer2[idx1];
            if (baseTex != BASE_GRASS_TEXTURE_INDEX || _grassSpriteTexture == null)
                return;

            // Calculate distance to the center of the tile
            Vector3 camPos = Camera.Instance.Position;
            float tileCenterX = (xf + 0.5f * lodf) * Constants.TERRAIN_SCALE;
            float tileCenterY = (yf + 0.5f * lodf) * Constants.TERRAIN_SCALE;
            float dx = camPos.X - tileCenterX;
            float dy = camPos.Y - tileCenterY;
            float distSq = dx * dx + dy * dy;

            // Outside grass range → do not draw anything
            if (distSq > GRASS_FAR_SQ)
                return;

            Color tileLight = (idx1 < _backTerrainLight.Length) ? _backTerrainLight[idx1] : Color.White;
            float windBase = GetWindValue(xi, yi);

            // Adjust grass amount per tile
            int grassPerTile = distSq < GRASS_NEAR * GRASS_NEAR ? 12 :
                               distSq < GRASS_FAR * GRASS_FAR ? 4 : 1;

            const float GRASS_U_WIDTH = 0.30f;
            const float SCALE_MIN = 1.0f, SCALE_MAX = 3.0f;
            const float ROT_JITTER_DEG = 90f;
            const float HEIGHT_OFFSET = 55f;

            for (int i = 0; i < grassPerTile; ++i)
            {
                // Random UV slice
                float u0 = PseudoRandom(xi, yi, 123 + i) * (1f - GRASS_U_WIDTH);
                float u1 = u0 + GRASS_U_WIDTH;

                // Random offset within the tile
                float halfUV = GRASS_U_WIDTH * 0.5f;
                float maxOffset = 0.5f - halfUV;

                float rx = (PseudoRandom(xi, yi, 17 + i) * 2f - 1f) * maxOffset;
                float ry = (PseudoRandom(xi, yi, 91 + i) * 2f - 1f) * maxOffset;

                float worldX = (xf + 0.5f * lodf + rx * lodf) * Constants.TERRAIN_SCALE;
                float worldY = (yf + 0.5f * lodf + ry * lodf) * Constants.TERRAIN_SCALE;
                float h = RequestTerrainHeight(worldX, worldY);

                // Random scale and rotation jitter
                float scale = MathHelper.Lerp(SCALE_MIN, SCALE_MAX, PseudoRandom(xi, yi, 33 + i));
                float jitter = MathHelper.ToRadians((PseudoRandom(xi, yi, 57 + i) - 0.5f) * 2f * ROT_JITTER_DEG);
                float windZ = MathHelper.ToRadians(windBase * 0.05f) + jitter;

                // --- add to buffer ---
                RenderGrassQuad(
                    new Vector3(worldX, worldY, h + HEIGHT_OFFSET),
                    lodf * scale,
                    windZ,
                    tileLight,
                    u0, u1);
            }
        }

        private void RenderGrassQuad(
            Vector3 position,
            float lodFactor,
            float windRotationZ,
            Color lightColor,
            float u0,
            float u1)
        {
            if (_grassSpriteTexture == null) return;

            const float BASE_W = 130f, BASE_H = 30f;
            float w = BASE_W * (u1 - u0) * lodFactor;
            float h = BASE_H * lodFactor;
            float hw = w * 0.5f;

            // local vertices
            Vector3 p1 = new(-hw, 0, 0), p2 = new(hw, 0, 0),
                    p3 = new(-hw, 0, h), p4 = new(hw, 0, h);

            Vector2 t1 = new(u0, 1), t2 = new(u1, 1),
                    t3 = new(u0, 0), t4 = new(u1, 0);

            Matrix world =
                Matrix.CreateRotationZ(MathHelper.ToRadians(45f) + windRotationZ) *
                Matrix.CreateTranslation(position);

            Vector3 wp1 = Vector3.Transform(p1, world);
            Vector3 wp2 = Vector3.Transform(p2, world);
            Vector3 wp3 = Vector3.Transform(p3, world);
            Vector3 wp4 = Vector3.Transform(p4, world);

            Vector3 c3 = lightColor.ToVector3() * GrassBrightness;
            c3 = Vector3.Min(c3, Vector3.One);
            Color final = new Color(c3);

            // --- copy to batched buffer ---
            if (_grassBatchCount + 6 >= GRASS_BATCH_VERTS)
                FlushGrassBatch();                           // out of space → flush

            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp1, final, t1);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp2, final, t2);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp3, final, t3);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp2, final, t2);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp4, final, t4);
            _grassBatch[_grassBatchCount++] = new VertexPositionColorTexture(wp3, final, t3);
        }

        private static float PseudoRandom(int x, int y, int salt = 0)
        {
            // 32-bit "Xorshift*" hash - fast and without libraries
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
            h ^= h >> 13; h *= 0x165667B1u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;       // 0 .. 1
        }
        private void PrepareTileVertices(int xi, int yi, float xf, float yf, int idx1, int idx2, int idx3, int idx4, float lodf)
        {
            float terrainHeight1 = idx1 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx1].B * 1.5f;
            float terrainHeight2 = idx2 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx2].B * 1.5f;
            float terrainHeight3 = idx3 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx3].B * 1.5f;
            float terrainHeight4 = idx4 >= _backTerrainHeight.Length ? 0f : _backTerrainHeight[idx4].B * 1.5f;

            float sx = xf * Constants.TERRAIN_SCALE;
            float sy = yf * Constants.TERRAIN_SCALE;
            float scaledSize = Constants.TERRAIN_SCALE * lodf;

            _tempTerrainVertex[0].X = sx;
            _tempTerrainVertex[0].Y = sy;
            _tempTerrainVertex[0].Z = terrainHeight1;

            _tempTerrainVertex[1].X = sx + scaledSize;
            _tempTerrainVertex[1].Y = sy;
            _tempTerrainVertex[1].Z = terrainHeight2;

            _tempTerrainVertex[2].X = sx + scaledSize;
            _tempTerrainVertex[2].Y = sy + scaledSize;
            _tempTerrainVertex[2].Z = terrainHeight3;

            _tempTerrainVertex[3].X = sx;
            _tempTerrainVertex[3].Y = sy + scaledSize;
            _tempTerrainVertex[3].Z = terrainHeight4;

            if (idx1 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx1].HasFlag(TWFlags.Height))
                _tempTerrainVertex[0].Z += SpecialHeight;
            if (idx2 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx2].HasFlag(TWFlags.Height))
                _tempTerrainVertex[1].Z += SpecialHeight;
            if (idx3 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx3].HasFlag(TWFlags.Height))
                _tempTerrainVertex[2].Z += SpecialHeight;
            if (idx4 < _terrain.TerrainWall.Length && _terrain.TerrainWall[idx4].HasFlag(TWFlags.Height))
                _tempTerrainVertex[3].Z += SpecialHeight;
        }

        private void PrepareTileLights(int idx1, int idx2, int idx3, int idx4)
        {
            _tempTerrainLights[0] = idx1 < _backTerrainLight.Length ? _backTerrainLight[idx1] : Color.Black;
            _tempTerrainLights[1] = idx2 < _backTerrainLight.Length ? _backTerrainLight[idx2] : Color.Black;
            _tempTerrainLights[2] = idx3 < _backTerrainLight.Length ? _backTerrainLight[idx3] : Color.Black;
            _tempTerrainLights[3] = idx4 < _backTerrainLight.Length ? _backTerrainLight[idx4] : Color.Black;
        }

        private void ApplyAlphaToLights(byte alpha1, byte alpha2, byte alpha3, byte alpha4)
        {
            _tempTerrainLights[0] *= alpha1 / 255f;
            _tempTerrainLights[1] *= alpha2 / 255f;
            _tempTerrainLights[2] *= alpha3 / 255f;
            _tempTerrainLights[3] *= alpha4 / 255f;

            _tempTerrainLights[0].A = alpha1;
            _tempTerrainLights[1].A = alpha2;
            _tempTerrainLights[2].A = alpha3;
            _tempTerrainLights[3].A = alpha4;
        }

        private void RenderTexture(int textureIndex, float xf, float yf, float lodScale = 1.0f)
        {
            if (Status != Models.GameControlStatus.Ready ||
                textureIndex == 255 ||
                textureIndex < 0 ||
                textureIndex >= _textures.Length ||
                _textures[textureIndex] == null)
                return;

            var texture = _textures[textureIndex];

            float baseWidth = 64f / texture.Width;
            float baseHeight = 64f / texture.Height;
            float suf = xf * baseWidth;
            float svf = yf * baseHeight;
            float uvWidth = baseWidth * lodScale;
            float uvHeight = baseHeight * lodScale;

            if (textureIndex == 5) // TileWater01
            {
                Vector2 flowOffset = _waterFlowDir * waterTotal;

                float wrapPeriod = (float)(2 * Math.PI / Math.Max(0.0001f, DistortionFrequency));
                float waterPhase = waterTotal % wrapPeriod;

                // 0
                _terrainTextureCoord[0].X = suf + flowOffset.X +
                                            (float)Math.Sin((suf + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoord[0].Y = svf + flowOffset.Y +
                                            (float)Math.Cos((svf + waterPhase) * DistortionFrequency) * DistortionAmplitude;

                // 1
                _terrainTextureCoord[1].X = suf + uvWidth + flowOffset.X +
                                            (float)Math.Sin((suf + uvWidth + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoord[1].Y = svf + flowOffset.Y +
                                            (float)Math.Cos((svf + waterPhase) * DistortionFrequency) * DistortionAmplitude;

                // 2
                _terrainTextureCoord[2].X = suf + uvWidth + flowOffset.X +
                                            (float)Math.Sin((suf + uvWidth + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoord[2].Y = svf + uvHeight + flowOffset.Y +
                                            (float)Math.Cos((svf + uvHeight + waterPhase) * DistortionFrequency) * DistortionAmplitude;

                // 3
                _terrainTextureCoord[3].X = suf + flowOffset.X +
                                            (float)Math.Sin((suf + waterPhase) * DistortionFrequency) * DistortionAmplitude;
                _terrainTextureCoord[3].Y = svf + uvHeight + flowOffset.Y +
                                            (float)Math.Cos((svf + uvHeight + waterPhase) * DistortionFrequency) * DistortionAmplitude;
            }
            else
            {
                _terrainTextureCoord[0].X = suf;
                _terrainTextureCoord[0].Y = svf;
                _terrainTextureCoord[1].X = suf + uvWidth;
                _terrainTextureCoord[1].Y = svf;
                _terrainTextureCoord[2].X = suf + uvWidth;
                _terrainTextureCoord[2].Y = svf + uvHeight;
                _terrainTextureCoord[3].X = suf;
                _terrainTextureCoord[3].Y = svf + uvHeight;
            }

            _terrainVertices[0].Position = _tempTerrainVertex[0];
            _terrainVertices[0].Color = _tempTerrainLights[0];
            _terrainVertices[0].TextureCoordinate = _terrainTextureCoord[0];

            _terrainVertices[1].Position = _tempTerrainVertex[1];
            _terrainVertices[1].Color = _tempTerrainLights[1];
            _terrainVertices[1].TextureCoordinate = _terrainTextureCoord[1];

            _terrainVertices[2].Position = _tempTerrainVertex[2];
            _terrainVertices[2].Color = _tempTerrainLights[2];
            _terrainVertices[2].TextureCoordinate = _terrainTextureCoord[2];

            _terrainVertices[3].Position = _tempTerrainVertex[2];
            _terrainVertices[3].Color = _tempTerrainLights[2];
            _terrainVertices[3].TextureCoordinate = _terrainTextureCoord[2];

            _terrainVertices[4].Position = _tempTerrainVertex[3];
            _terrainVertices[4].Color = _tempTerrainLights[3];
            _terrainVertices[4].TextureCoordinate = _terrainTextureCoord[3];

            _terrainVertices[5].Position = _tempTerrainVertex[0];
            _terrainVertices[5].Color = _tempTerrainLights[0];
            _terrainVertices[5].TextureCoordinate = _terrainTextureCoord[0];

            GraphicsManager.Instance.BasicEffect3D.Texture = texture;
            foreach (var pass in GraphicsManager.Instance.BasicEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, 2);
            }
        }
    }
}