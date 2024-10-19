using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Main.Content;
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
        private const int BlockSize = 4; // blockSize
        private TerrainAttribute _terrain;
        private BasicEffect _terrainEffect;
        private TerrainMapping _mapping;
        private Texture2D[] _textures;
        private float[] _terrainGrassWind;
        private Color[] _backTerrainLight;
        private Vector3[] _terrainNormal;
        private Color[] _backTerrainHeight;
        private Color[] _terrainLightData;

        // Pre-allocated buffer for vertices to reduce memory allocations
        private readonly VertexPositionColorTexture[] _terrainVertices = new VertexPositionColorTexture[6];

        private const float WindScale = 10f;

        public short WorldIndex { get; set; }
        public Vector3 Light { get; set; } = new Vector3(0.5f, -0.5f, 0.5f);

        public TerrainControl()
        {
            AutoSize = false;
            Width = MuGame.Instance.Width;
            Height = MuGame.Instance.Height;
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

            _terrainEffect = new BasicEffect(GraphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                World = Matrix.Identity
            };

            tasks.Add(terrainReader.Load(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att"))
                .ContinueWith(t => _terrain = t.Result));
            tasks.Add(ozbReader.Load(Path.Combine(fullPathWorldFolder, $"TerrainHeight.OZB"))
                .ContinueWith(t => _backTerrainHeight = t.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            tasks.Add(mappingReader.Load(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map"))
                .ContinueWith(t => _mapping = t.Result));

            var textureMapFiles = new string[256];

            var initialTextures = new Dictionary<int, string>
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
                { 10, "AlphaTile01.Tga" },
                { 11, "TileRock05.ozj" },
                { 12, "TileRock06.ozj" },
                { 13, "TileRock07.ozj" },
                { 30, "TileGrass01.tga" },
                { 31, "TileGrass02.tga" },
                { 32, "TileGrass03.tga" },
                { 100, "leaf01.jpg" },
                { 101, "leaf02.jpg" },
                { 102, Path.Combine("World1", "rain01.tga") },
                { 103, Path.Combine("World1", "rain02.tga") },
                { 104, Path.Combine("World1", "rain03.tga") }
            };

            foreach (var kvp in initialTextures)
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

            if (_terrainEffect != null)
            {
                _terrainEffect.Projection = Camera.Instance.Projection;
                _terrainEffect.View = Camera.Instance.View;
                InitTerrainLight(time);
            }
        }

        public override void Draw(GameTime time)
        {
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
            if (_terrain?.TerrainWall == null || xf < 0.0f || yf < 0.0f)
                return 0.0f;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int index = GetTerrainIndex((int)xf, (int)yf);

            if (index >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || _terrain.TerrainWall[index].HasFlag(TWFlags.Height))
                return SpecialHeight;

            int xi = (int)xf;
            int yi = (int)yf;
            float xd = xf - xi;
            float yd = yf - yi;

            int index1 = GetTerrainIndexRepeat(xi, yi);
            int index2 = GetTerrainIndexRepeat(xi, yi + 1);
            int index3 = GetTerrainIndexRepeat(xi + 1, yi);
            int index4 = GetTerrainIndexRepeat(xi + 1, yi + 1);

            if (new[] { index1, index2, index3, index4 }.Any(i => i >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE))
                return SpecialHeight;

            float left = MathHelper.Lerp(_backTerrainHeight[index1].B, _backTerrainHeight[index2].B, yd);
            float right = MathHelper.Lerp(_backTerrainHeight[index3].B, _backTerrainHeight[index4].B, yd);
            return MathHelper.Lerp(left, right, xd);
        }

        public Vector3 RequestTerrainLight(float xf, float yf)
        {
            if (_terrain?.TerrainWall == null || xf < 0.0f || yf < 0.0f)
                return Vector3.Zero;

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

            if (new[] { index1, index2, index3, index4 }.Any(i => i >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE))
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

        public override void Dispose()
        {
            base.Dispose();

            _terrainEffect?.Dispose();

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

        private void InitTerrainLight(GameTime time)
        {
            float windSpeed = (float)((time.TotalGameTime.TotalMilliseconds % 720000) * 0.002); // Upraszczony modulo

            for (int y = 0; y <= Math.Min(253, Constants.TERRAIN_SIZE_MASK); y++)
            {
                for (int x = 0; x <= Math.Min(253, Constants.TERRAIN_SIZE_MASK); x++)
                {
                    int index = GetTerrainIndex(x, y);
                    _terrainGrassWind[index] = (float)Math.Sin(windSpeed + x * 5f) * WindScale;
                }
            }
        }

        private void RenderTerrain(bool isAfter)
        {
            if (_terrainEffect == null) return;

            for (int yi = 0; yi < Constants.TERRAIN_SIZE_MASK; yi += BlockSize)
            {
                for (int xi = 0; xi < Constants.TERRAIN_SIZE_MASK; xi += BlockSize)
                {
                    float xStart = xi * Constants.TERRAIN_SCALE;
                    float yStart = yi * Constants.TERRAIN_SCALE;
                    float xEnd = (xi + BlockSize) * Constants.TERRAIN_SCALE;
                    float yEnd = (yi + BlockSize) * Constants.TERRAIN_SCALE;

                    float minZ = float.MaxValue;
                    float maxZ = float.MinValue;

                    for (int y = yi; y < yi + BlockSize; y++)
                    {
                        for (int x = xi; x < xi + BlockSize; x++)
                        {
                            int index = GetTerrainIndexRepeat(x, y);
                            float height = _backTerrainHeight[index].B * 1.5f;

                            if (height < minZ) minZ = height;
                            if (height > maxZ) maxZ = height;
                        }
                    }

                    var blockBounds = new BoundingBox(
                        new Vector3(xStart, yStart, minZ),
                        new Vector3(xEnd, yEnd, maxZ)
                    );

                    if (Camera.Instance.Frustum.Intersects(blockBounds))
                        RenderTerrainBlock(xStart / Constants.TERRAIN_SCALE, yStart / Constants.TERRAIN_SCALE, xi, yi, isAfter);
                }
            }
        }

        private void RenderTerrainBlock(float xf, float yf, int xi, int yi, bool isAfter)
        {
            int lodi = 1;
            var lodf = (float)lodi;

            for (int i = 0; i < BlockSize; i += lodi)
            {
                for (int j = 0; j < BlockSize; j += lodi)
                {

                    RenderTerrainTile(xf + j, yf + i, xi + j, yi + i, lodf, lodi, isAfter);
                }
            }
        }

        private void RenderTerrainTile(float xf, float yf, int xi, int yi, float lodf, int lodi, bool isAfter)
        {
            if (isAfter)
                return;

            int idx1 = GetTerrainIndex(xi, yi);

            if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.NoGround))
                return;

            int idx2 = GetTerrainIndex(xi + lodi, yi);
            int idx3 = GetTerrainIndex(xi + lodi, yi + lodi);
            int idx4 = GetTerrainIndex(xi, yi + lodi);

            byte alpha1 = _mapping.Alpha[idx1];
            byte alpha2 = _mapping.Alpha[idx2];
            byte alpha3 = _mapping.Alpha[idx3];
            byte alpha4 = _mapping.Alpha[idx4];

            bool isOpaque = alpha1 >= 1 && alpha2 >= 1 && alpha3 >= 1 && alpha4 >= 1;
            bool hasAlpha = alpha1 > 0 || alpha2 > 0 || alpha3 > 0 || alpha4 > 0;

            float terrainHeight1 = _backTerrainHeight[idx1].B * 1.5f;
            float terrainHeight2 = _backTerrainHeight[idx2].B * 1.5f;
            float terrainHeight3 = _backTerrainHeight[idx3].B * 1.5f;
            float terrainHeight4 = _backTerrainHeight[idx4].B * 1.5f;

            float sx = xf * Constants.TERRAIN_SCALE;
            float sy = yf * Constants.TERRAIN_SCALE;

            var terrainVertex = new Vector3[4]
            {
                new Vector3(sx, sy, terrainHeight1),
                new Vector3(sx + Constants.TERRAIN_SCALE * lodf, sy, terrainHeight2),
                new Vector3(sx + Constants.TERRAIN_SCALE * lodf, sy + Constants.TERRAIN_SCALE * lodf, terrainHeight3),
                new Vector3(sx, sy + Constants.TERRAIN_SCALE * lodf, terrainHeight4)
            };

            if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.Height))
                terrainVertex[0].Z += 1200f;

            if (_terrain.TerrainWall[idx2].HasFlag(TWFlags.Height))
                terrainVertex[1].Z += 1200f;

            if (_terrain.TerrainWall[idx3].HasFlag(TWFlags.Height))
                terrainVertex[2].Z += 1200f;

            if (_terrain.TerrainWall[idx4].HasFlag(TWFlags.Height))
                terrainVertex[3].Z += 1200f;

            var terrainLights = new Color[4]
            {
                _backTerrainLight[idx1],
                _backTerrainLight[idx2],
                _backTerrainLight[idx3],
                _backTerrainLight[idx4]
            };

            if (isOpaque)
            {
                GraphicsDevice.BlendState = BlendState.Opaque;
                RenderTexture(_mapping.Layer2[idx1], xf, yf, terrainVertex, terrainLights);
            }
            else
            {
                GraphicsDevice.BlendState = BlendState.Opaque;
                RenderTexture(_mapping.Layer1[idx1], xf, yf, terrainVertex, terrainLights);
            }

            if (hasAlpha && !isOpaque)
            {
                terrainLights[0] *= alpha1 / 255f;
                terrainLights[1] *= alpha2 / 255f;
                terrainLights[2] *= alpha3 / 255f;
                terrainLights[3] *= alpha4 / 255f;

                terrainLights[0].A = alpha1;
                terrainLights[1].A = alpha2;
                terrainLights[2].A = alpha3;
                terrainLights[3].A = alpha4;

                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                RenderTexture(_mapping.Layer2[idx1], xf, yf, terrainVertex, terrainLights);
            }
        }

        private void RenderTexture(int textureIndex, float xf, float yf, Vector3[] terrainVertex, Color[] terrainLights)
        {
            if (textureIndex < 0 || textureIndex >= _textures.Length)
                return;

            if (_textures[textureIndex] == null)
                return;

            var texture = _textures[textureIndex];

            var width = 64f / texture.Width;
            var height = 64f / texture.Height;

            float suf = xf * width;
            float svf = yf * height;

            var terrainTextureCoord = new Vector2[4]
            {
                new Vector2(suf, svf),
                new Vector2(suf + width, svf),
                new Vector2(suf + width, svf + height),
                new Vector2(suf, svf + height)
            };

            _terrainVertices[0] = new VertexPositionColorTexture(terrainVertex[0], terrainLights[0], terrainTextureCoord[0]);
            _terrainVertices[1] = new VertexPositionColorTexture(terrainVertex[1], terrainLights[1], terrainTextureCoord[1]);
            _terrainVertices[2] = new VertexPositionColorTexture(terrainVertex[2], terrainLights[2], terrainTextureCoord[2]);

            _terrainVertices[3] = new VertexPositionColorTexture(terrainVertex[2], terrainLights[2], terrainTextureCoord[2]);
            _terrainVertices[4] = new VertexPositionColorTexture(terrainVertex[3], terrainLights[3], terrainTextureCoord[3]);
            _terrainVertices[5] = new VertexPositionColorTexture(terrainVertex[0], terrainLights[0], terrainTextureCoord[0]);

            _terrainEffect.Texture = texture;

            foreach (var pass in _terrainEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, 2);
            }
        }
    }
}
