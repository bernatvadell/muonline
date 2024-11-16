using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.CAP;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public class TerrainControl : GameControl
    {
        private const float SpecialHeight = 1200f;
        private const int BlockSize = 4;
        private const int MAX_LOD_LEVELS = 2;
        private const float LOD_DISTANCE_MULTIPLIER = 3000f;
        private const float WindScale = 10f;
        private const int UPDATE_INTERVAL_MS = 32;
        private const float CAMERA_MOVE_THRESHOLD = 32f;

        private TerrainAttribute _terrain;
        private TerrainMapping _mapping;
        private Texture2D[] _textures;
        private float[] _terrainGrassWind;
        private Color[] _backTerrainLight;
        private Vector3[] _terrainNormal;
        private Color[] _backTerrainHeight;
        private Color[] _terrainLightData;

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
            { 103,  "rain02.ozt" },
            { 104,  "rain03.ozt" }
        };

        private readonly VertexPositionColorTexture[] _terrainVertices = new VertexPositionColorTexture[6];
        private readonly Vector2[] _terrainTextureCoord;
        private readonly Vector3[] _tempTerrainVertex;
        private readonly Color[] _tempTerrainLights;

        private float _lastWindSpeed = float.MinValue;
        private double _lastUpdateTime;

        private readonly int[] LOD_STEPS = { 1, 4 };
        private readonly WindCache _windCache = new WindCache();

        private readonly TerrainBlockCache _blockCache;
        private readonly Queue<TerrainBlock> _visibleBlocks = new Queue<TerrainBlock>(64);
        private Vector2 _lastCameraPosition;

        public TerrainControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
            _blockCache = new TerrainBlockCache(BlockSize, Constants.TERRAIN_SIZE);
            _terrainVertices = new VertexPositionColorTexture[6];
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

            if (!Directory.Exists(fullPathWorldFolder))
                return;

            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            tasks.Add(terrainReader.Load(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att"))
                .ContinueWith(t => _terrain = t.Result));
            tasks.Add(ozbReader.Load(Path.Combine(fullPathWorldFolder, $"TerrainHeight.OZB"))
                .ContinueWith(t => _backTerrainHeight = t.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            tasks.Add(mappingReader.Load(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map"))
                .ContinueWith(t => _mapping = t.Result));

            var textureMapFiles = new string[256];

            foreach (var kvp in TextureMappingFiles)
            {
                textureMapFiles[kvp.Key] = Path.Combine(fullPathWorldFolder, kvp.Value);
            }

            for (int i = 1; i <= 16; i++)
            {
                textureMapFiles[13 + i] = Path.Combine(fullPathWorldFolder, $"ExtTile{i:00}.ozj");
            }

            _textures = new Texture2D[textureMapFiles.Length];

            for (int t = 0; t < textureMapFiles.Length; t++)
            {
                var path = textureMapFiles[t];
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                int textureIndex = t;
                tasks.Add(TextureLoader.Instance.Prepare(path)
                    .ContinueWith(_ => _textures[textureIndex] = TextureLoader.Instance.GetTexture2D(path)));
            }

            var textureLightPath = Path.Combine(fullPathWorldFolder, "TerrainLight.OZB");

            if (File.Exists(textureLightPath))
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

            CreateTerrainNormal();
            CreateTerrainLight();

            await base.Load();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (Status != Models.GameControlStatus.Ready)
                return;

            InitTerrainLight(time);
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

        public TWFlags RequestTerraingFlag(int x, int y) => _terrain.TerrainWall[GetTerrainIndex(x, y)];

        public float RequestTerrainHeight(float xf, float yf)
        {
            if (_terrain?.TerrainWall == null || xf < 0.0f || yf < 0.0f || _backTerrainHeight == null || float.IsNaN(xf) || float.IsNaN(yf))
                return 0.0f;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int index = GetTerrainIndex((int)xf, (int)yf);

            if (index >= _backTerrainHeight.Length || _terrain.TerrainWall[index].HasFlag(TWFlags.Height))
                return SpecialHeight;

            int xi = (int)xf;
            int yi = (int)yf;
            float xd = xf - xi;
            float yd = yf - yi;

            int index1 = GetTerrainIndexRepeat(xi, yi);
            int index2 = GetTerrainIndexRepeat(xi, yi + 1);
            int index3 = GetTerrainIndexRepeat(xi + 1, yi);
            int index4 = GetTerrainIndexRepeat(xi + 1, yi + 1);

            if (new[] { index1, index2, index3, index4 }.Any(i => i >= _backTerrainHeight.Length))
                return SpecialHeight;

            float left = MathHelper.Lerp(_backTerrainHeight[index1].B, _backTerrainHeight[index2].B, yd);
            float right = MathHelper.Lerp(_backTerrainHeight[index3].B, _backTerrainHeight[index4].B, yd);

            return MathHelper.Lerp(left, right, xd);
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

        private void InitTerrainLight(GameTime time)
        {
            if (_terrainGrassWind == null) return;

            if (time.TotalGameTime.TotalMilliseconds - _lastUpdateTime < UPDATE_INTERVAL_MS)
            {
                return;
            }

            float windSpeed = (float)(time.TotalGameTime.TotalMilliseconds % 720000 * 0.002);

            if (Math.Abs(windSpeed - _lastWindSpeed) < 0.01f)
            {
                return;
            }

            _lastWindSpeed = windSpeed;
            _lastUpdateTime = time.TotalGameTime.TotalMilliseconds;

            var camera = Camera.Instance;
            int startX = Math.Max(0, (int)(camera.Position.X / Constants.TERRAIN_SCALE) - 32);
            int startY = Math.Max(0, (int)(camera.Position.Y / Constants.TERRAIN_SCALE) - 32);
            int endX = Math.Min(255, startX + 64);
            int endY = Math.Min(255, startY + 64);

            System.Numerics.Vector<float> windScaleVector = new System.Numerics.Vector<float>(WindScale);
            int vectorSize = System.Numerics.Vector<float>.Count;

            for (int y = startY; y <= endY; y++)
            {
                int baseIndex = GetTerrainIndex(startX, y);

                for (int x = startX; x <= endX - vectorSize; x += vectorSize)
                {
                    var phases = new float[vectorSize];
                    for (int i = 0; i < vectorSize; i++)
                    {
                        phases[i] = _windCache.FastSin(windSpeed + (x + i) * 5f);
                    }

                    var windVector = new System.Numerics.Vector<float>(phases) * windScaleVector;
                    windVector.CopyTo(_terrainGrassWind, baseIndex + x - startX);
                }

                for (int x = endX - (endX % vectorSize); x <= endX; x++)
                {
                    int index = GetTerrainIndex(x, y);
                    _terrainGrassWind[index] = _windCache.FastSin(windSpeed + x * 5f) * WindScale;
                }
            }
        }

        private void RenderTerrain(bool isAfter)
        {
            if (_backTerrainHeight == null) return;

            var cameraPosition = new Vector2(Camera.Instance.Position.X, Camera.Instance.Position.Y);
            UpdateVisibleBlocks(cameraPosition);

            foreach (var block in _visibleBlocks)
            {
                if (block.IsVisible)
                {
                    float xStart = block.Xi * Constants.TERRAIN_SCALE;
                    RenderTerrainBlock(
                        xStart / Constants.TERRAIN_SCALE,
                        block.Yi * Constants.TERRAIN_SCALE / Constants.TERRAIN_SCALE,
                        block.Xi,
                        block.Yi,
                        isAfter,
                        LOD_STEPS[block.LODLevel]
                    );
                }
            }
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

        private void UpdateVisibleBlocks(Vector2 cameraPosition)
        {
            if (Vector2.Distance(_lastCameraPosition, cameraPosition) < CAMERA_MOVE_THRESHOLD)
                return;

            _lastCameraPosition = cameraPosition;
            _visibleBlocks.Clear();

            float renderDistance = Camera.Instance.ViewFar * 1.5f;

            const int EXTRA_BLOCKS_MARGIN = 2;

            int startX = Math.Max(0, (int)((cameraPosition.X - renderDistance) / (Constants.TERRAIN_SCALE * BlockSize)) - EXTRA_BLOCKS_MARGIN);
            int startY = Math.Max(0, (int)((cameraPosition.Y - renderDistance) / (Constants.TERRAIN_SCALE * BlockSize)) - EXTRA_BLOCKS_MARGIN);
            int endX = Math.Min(Constants.TERRAIN_SIZE / BlockSize - 1, (int)((cameraPosition.X + renderDistance) / (Constants.TERRAIN_SCALE * BlockSize)) + EXTRA_BLOCKS_MARGIN);
            int endY = Math.Min(Constants.TERRAIN_SIZE / BlockSize - 1, (int)((cameraPosition.Y + renderDistance) / (Constants.TERRAIN_SCALE * BlockSize)) + EXTRA_BLOCKS_MARGIN);

            var vectorSize = System.Numerics.Vector<float>.Count;
            var heightBuffer = new float[BlockSize * BlockSize];

            for (int gridY = startY; gridY <= endY; gridY++)
            {
                for (int gridX = startX; gridX <= endX; gridX++)
                {
                    var block = _blockCache.GetBlock(gridX, gridY);

                    float xStart = block.Xi * Constants.TERRAIN_SCALE;
                    float yStart = block.Yi * Constants.TERRAIN_SCALE;
                    float xEnd = (block.Xi + BlockSize) * Constants.TERRAIN_SCALE;
                    float yEnd = (block.Yi + BlockSize) * Constants.TERRAIN_SCALE;

                    block.Center = new Vector2((xStart + xEnd) * 0.5f, (yStart + yEnd) * 0.5f);
                    float distanceToCamera = Vector2.Distance(block.Center, cameraPosition);
                    block.LODLevel = GetLODLevel(distanceToCamera);

                    if (distanceToCamera <= renderDistance * 1.2f)
                    {
                        int idx = 0;
                        for (int y = 0; y < BlockSize; y++)
                        {
                            for (int x = 0; x < BlockSize; x++)
                            {
                                int terrainIndex = GetTerrainIndexRepeat(block.Xi + x, block.Yi + y);
                                heightBuffer[idx++] = _backTerrainHeight[terrainIndex].B * 1.5f;
                            }
                        }

                        var minVector = new System.Numerics.Vector<float>(float.MaxValue);
                        var maxVector = new System.Numerics.Vector<float>(float.MinValue);

                        for (int i = 0; i < heightBuffer.Length; i += vectorSize)
                        {
                            var heightVector = new System.Numerics.Vector<float>(heightBuffer, i);
                            minVector = System.Numerics.Vector.Min(minVector, heightVector);
                            maxVector = System.Numerics.Vector.Max(maxVector, heightVector);
                        }

                        block.MinZ = minVector[0];
                        block.MaxZ = maxVector[0];
                        for (int i = 1; i < vectorSize; i++)
                        {
                            block.MinZ = Math.Min(block.MinZ, minVector[i]);
                            block.MaxZ = Math.Max(block.MaxZ, maxVector[i]);
                        }

                        block.Bounds = new BoundingBox(
                            new Vector3(xStart, yStart, block.MinZ),
                            new Vector3(xEnd, yEnd, block.MaxZ)
                        );

                        block.IsVisible = Camera.Instance.Frustum.Contains(block.Bounds) != ContainmentType.Disjoint;
                        if (block.IsVisible)
                        {
                            _visibleBlocks.Enqueue(block);
                        }
                    }
                    else
                    {
                        block.IsVisible = false;
                    }
                }
            }
        }

        private void RenderTerrainBlock(float xf, float yf, int xi, int yi, bool isAfter, int lodStep)
        {
            if (BlockSize % lodStep != 0)
            {
                lodStep = 1;
            }

            GraphicsDevice.BlendState = BlendState.Opaque;
            var effect = GraphicsManager.Instance.BasicEffect3D;
            effect.Projection = Camera.Instance.Projection;
            effect.View = Camera.Instance.View;

            for (int i = 0; i < BlockSize; i += lodStep)
            {
                for (int j = 0; j < BlockSize; j += lodStep)
                {
                    RenderTerrainTile(xf + j, yf + i, xi + j, yi + i, (float)lodStep, lodStep, isAfter);
                }
            }
        }

        private void RenderTerrainTile(float xf, float yf, int xi, int yi, float lodf, int lodi, bool isAfter)
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

            float lodScale = lodf;

            byte alpha1 = idx1 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx1];
            byte alpha2 = idx2 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx2];
            byte alpha3 = idx3 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx3];
            byte alpha4 = idx4 >= _mapping.Alpha.Length ? (byte)0 : _mapping.Alpha[idx4];

            bool isOpaque = alpha1 >= 255 && alpha2 >= 255 && alpha3 >= 255 && alpha4 >= 255;
            bool hasAlpha = alpha1 > 0 || alpha2 > 0 || alpha3 > 0 || alpha4 > 0;

            if (isOpaque)
            {
                RenderTexture(_mapping.Layer2[idx1], xf, yf, lodScale);
            }
            else
            {
                RenderTexture(_mapping.Layer1[idx1], xf, yf, lodScale);
            }

            if (hasAlpha && !isOpaque)
            {
                ApplyAlphaToLights(alpha1, alpha2, alpha3, alpha4);
                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                RenderTexture(_mapping.Layer2[idx1], xf, yf, lodScale);
            }
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

            _terrainTextureCoord[0].X = suf;
            _terrainTextureCoord[0].Y = svf;
            _terrainTextureCoord[1].X = suf + uvWidth;
            _terrainTextureCoord[1].Y = svf;
            _terrainTextureCoord[2].X = suf + uvWidth;
            _terrainTextureCoord[2].Y = svf + uvHeight;
            _terrainTextureCoord[3].X = suf;
            _terrainTextureCoord[3].Y = svf + uvHeight;

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
