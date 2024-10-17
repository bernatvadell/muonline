using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.CWS;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Client.Main.Controls
{
    public class TerrainControl : GameControl
    {
        private readonly float _specialHeight = 1200f;

        private TerrainAttribute _terrain;
        private BasicEffect _terrainEffect;

        private TerrainMapping _mapping;
        private Texture2D[] _textures;

        private float[] _terrainGrassWind;
        private Color[] _backTerrainLight;
        private Vector3[] _terrainNormal;
        private Color[] _backTerrainHeight;
        private Color[] _terrainLightData;

        public short WorldIndex { get; set; }
        public Vector3 Light { get; set; } = new Vector3(0.5f, -0.5f, 0.5f);

        public override async Task Load()
        {
            var terrainReader = new ATTReader();
            var ozbReader = new OZBReader();
            var objReader = new OBJReader();
            var mapping = new MapReader();
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

            //_terrainEffect = new AlphaTestEffect(GraphicsDevice)
            //{
            //    VertexColorEnabled = true,
            //    World = Matrix.Identity,
            //    AlphaFunction = CompareFunction.Greater,
            //    ReferenceAlpha = (int)(255 * 0.25f)
            //};

            tasks.Add(terrainReader.Load(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.att")).ContinueWith(t => _terrain = t.Result));
            tasks.Add(ozbReader.Load(Path.Combine(fullPathWorldFolder, $"TerrainHeight.OZB")).ContinueWith(t => _backTerrainHeight = t.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            tasks.Add(mapping.Load(Path.Combine(fullPathWorldFolder, $"EncTerrain{WorldIndex}.map")).ContinueWith(t => _mapping = t.Result));

            var textureMapFiles = new string[256];

            textureMapFiles[0] = Path.Combine(fullPathWorldFolder, "TileGrass01.ozj");
            textureMapFiles[1] = Path.Combine(fullPathWorldFolder, "TileGrass02.ozj");
            textureMapFiles[2] = Path.Combine(fullPathWorldFolder, "TileGround01.ozj");
            textureMapFiles[3] = Path.Combine(fullPathWorldFolder, "TileGround02.ozj");
            textureMapFiles[4] = Path.Combine(fullPathWorldFolder, "TileGround03.ozj");
            textureMapFiles[5] = Path.Combine(fullPathWorldFolder, "TileWater01.ozj");
            textureMapFiles[6] = Path.Combine(fullPathWorldFolder, "TileWood01.ozj");
            textureMapFiles[7] = Path.Combine(fullPathWorldFolder, "TileRock01.ozj");
            textureMapFiles[8] = Path.Combine(fullPathWorldFolder, "TileRock02.ozj");
            textureMapFiles[9] = Path.Combine(fullPathWorldFolder, "TileRock03.ozj");
            textureMapFiles[10] = Path.Combine(fullPathWorldFolder, "AlphaTile01.Tga");
            textureMapFiles[11] = Path.Combine(fullPathWorldFolder, "TileRock05.ozj");
            textureMapFiles[12] = Path.Combine(fullPathWorldFolder, "TileRock06.ozj");
            textureMapFiles[13] = Path.Combine(fullPathWorldFolder, "TileRock07.ozj");

            for (int i = 1; i <= 16; i++)
                textureMapFiles[13 + i] = Path.Combine(fullPathWorldFolder, $"ExtTile{i.ToString().PadLeft(2, '0')}.ozj");

            textureMapFiles[13] = Path.Combine(fullPathWorldFolder, "TileRock07.ozj");

            textureMapFiles[30] = Path.Combine(fullPathWorldFolder, "TileGrass01.tga");
            textureMapFiles[31] = Path.Combine(fullPathWorldFolder, "TileGrass02.tga");
            textureMapFiles[32] = Path.Combine(fullPathWorldFolder, "TileGrass03.tga");

            textureMapFiles[100] = Path.Combine(fullPathWorldFolder, "leaf01.jpg");
            textureMapFiles[101] = Path.Combine(fullPathWorldFolder, "leaf02.jpg");

            textureMapFiles[102] = Path.Combine(Constants.DataPath, "World1", "rain01.tga");
            textureMapFiles[103] = Path.Combine(Constants.DataPath, "World1", "rain02.tga");
            textureMapFiles[104] = Path.Combine(Constants.DataPath, "World1", "rain03.tga");

            _textures = new Texture2D[textureMapFiles.Length];

            for (int t = 0; t < textureMapFiles.Length; t++)
            {
                var path = textureMapFiles[t];
                if (path == null || !File.Exists(path)) continue;
                var tt = t;
                tasks.Add(TextureLoader.Instance.Prepare(path).ContinueWith((_) => _textures[tt] = TextureLoader.Instance.GetTexture2D(path)));
            }

            var textureLightPath = Path.Combine(fullPathWorldFolder, "TerrainLight.OZB");

            if (File.Exists(textureLightPath))
                tasks.Add(ozbReader.Load(textureLightPath).ContinueWith((ozb) => _terrainLightData = ozb.Result.Data.Select(x => new Color(x.R, x.G, x.B)).ToArray()));
            else
            {
                _terrainLightData = new Color[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
                for (var i = 0; i < _terrainLightData.Length; i++)
                    _terrainLightData[i] = Color.White;
            }

            await Task.WhenAll(tasks);

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
            if (_terrain == null || _terrain.TerrainWall == null || xf < 0.0f || yf < 0.0f)
                return 0.0f;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int Index = (int)GetTerrainIndex((int)xf, (int)yf);

            if (Index >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)
                return _specialHeight;

            if (_terrain.TerrainWall[Index].HasFlag(TWFlags.Height))
                return _specialHeight;

            int xi = (int)xf;
            int yi = (int)yf;
            float xd = xf - xi;
            float yd = yf - yi;

            int Index1 = GetTerrainIndexRepeat(xi, yi);
            int Index2 = GetTerrainIndexRepeat(xi, yi + 1);
            int Index3 = GetTerrainIndexRepeat(xi + 1, yi);
            int Index4 = GetTerrainIndexRepeat(xi + 1, yi + 1);

            if (Index1 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || Index2 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE ||
                Index3 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || Index4 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)
                return _specialHeight;

            float left = _backTerrainHeight[Index1].B + (_backTerrainHeight[Index2].B - _backTerrainHeight[Index1].B) * yd;
            float right = _backTerrainHeight[Index3].B + (_backTerrainHeight[Index4].B - _backTerrainHeight[Index3].B) * yd;
            return left + (right - left) * xd;
        }

        public Vector3 RequestTerrainLight(float xf, float yf)
        {
            if (_terrain == null || _terrain.TerrainWall == null || xf < 0.0f || yf < 0.0f)
                return Vector3.Zero;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf;
            int yi = (int)yf;
            float xd = xf - xi;
            float yd = yf - yi;

            int Index1 = xi + yi * Constants.TERRAIN_SIZE;// GetTerrainIndexRepeat(xi, yi);
            int Index2 = xi + 1 + yi * Constants.TERRAIN_SIZE; // GetTerrainIndexRepeat(xi, yi + 1);
            int Index3 = xi + 1 + (yi + 1) * Constants.TERRAIN_SIZE; // GetTerrainIndexRepeat(xi + 1, yi);
            int Index4 = xi + (yi + 1) * Constants.TERRAIN_SIZE; // GetTerrainIndexRepeat(xi + 1, yi + 1);

            if (Index1 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || Index2 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE ||
                    Index3 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || Index4 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)
                return Vector3.Zero;

            var output = new float[3];

            for (var i = 0; i < 3; i++)
            {
                float left = 0;
                float right = 0;

                if (_backTerrainLight != null)
                {
                    switch (i)
                    {
                        case 0:
                            left = _backTerrainLight[Index1].R + (_backTerrainLight[Index4].R - _backTerrainLight[Index1].R) * yd;
                            right = _backTerrainLight[Index2].R + (_backTerrainLight[Index3].R - _backTerrainLight[Index2].R) * yd;
                            break;
                        case 1:
                            left = _backTerrainLight[Index1].G + (_backTerrainLight[Index4].G - _backTerrainLight[Index1].G) * yd;
                            right = _backTerrainLight[Index2].G + (_backTerrainLight[Index3].G - _backTerrainLight[Index2].G) * yd;
                            break;
                        case 2:
                            left = _backTerrainLight[Index1].B + (_backTerrainLight[Index4].B - _backTerrainLight[Index1].B) * yd;
                            right = _backTerrainLight[Index2].B + (_backTerrainLight[Index3].B - _backTerrainLight[Index2].B) * yd;
                            break;
                    }
                }

                output[i] = (left + (right - left) * xd);
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

        private static int GetTerrainIndex(int x, int y)
        {
            return (y) * Constants.TERRAIN_SIZE + (x);
        }
        private static int GetTerrainIndexRepeat(int x, int y)
        {
            return ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE) + (x & Constants.TERRAIN_SIZE_MASK);
        }

        private void CreateTerrainNormal()
        {
            _terrainNormal = new Vector3[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int Index = GetTerrainIndex(x, y);
                    Vector3 v1, v2, v3, v4;

                    v4 = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x, y)].B);
                    v1 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y)].B);
                    v2 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y + 1)].B);
                    v3 = new Vector3((x) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x, y + 1)].B);

                    Vector3 faceNormal = MathUtils.FaceNormalize(v1, v2, v3);
                    _terrainNormal[Index] = _terrainNormal[Index] + faceNormal;
                    faceNormal = MathUtils.FaceNormalize(v3, v4, v1);
                    _terrainNormal[Index] = _terrainNormal[Index] + faceNormal;
                }
            }
        }
        private void CreateTerrainLight()
        {
            _backTerrainLight = new Color[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int Index = GetTerrainIndex(x, y);
                    float luminosity = MathUtils.DotProduct(_terrainNormal[Index], Light) + 0.5f;
                    if (luminosity < 0f)
                        luminosity = 0f;
                    else if (luminosity > 1f)
                        luminosity = 1f;

                    _backTerrainLight[Index] = _terrainLightData[Index] * luminosity;
                }
            }
        }
        private void InitTerrainLight(GameTime time)
        {
            _terrainGrassWind = new float[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            float WindScale;
            float WindSpeed;

            WindScale = 10f;
            WindSpeed = (int)time.ElapsedGameTime.TotalMilliseconds % (360000 * 2) * (0.002f);

            var yi = 0;

            for (; yi <= Math.Min(250 + 3, Constants.TERRAIN_SIZE_MASK); yi += 1)
            {
                var xi = 0;
                var xf = (float)xi;
                for (; xi <= Math.Min(250 + 3, Constants.TERRAIN_SIZE_MASK); xi += 1, xf += 1f)
                {
                    int Index = GetTerrainIndex(xi, yi);
                    _terrainGrassWind[Index] = (float)Math.Sin(WindSpeed + xf * 5f) * WindScale;
                }
            }
        }
        private void RenderTerrain(bool isAfter)
        {
            if (_terrainEffect == null) return;

            int blockSize = 4;

            for (int yi = 0; yi <= Constants.TERRAIN_SIZE_MASK; yi += blockSize)
            {
                for (int xi = 0; xi <= Constants.TERRAIN_SIZE_MASK; xi += blockSize)
                {
                    float xStart = xi * Constants.TERRAIN_SCALE;
                    float yStart = yi * Constants.TERRAIN_SCALE;
                    float xEnd = (xi + blockSize) * Constants.TERRAIN_SCALE;
                    float yEnd = (yi + blockSize) * Constants.TERRAIN_SCALE;

                    float minZ = float.MaxValue;
                    float maxZ = float.MinValue;

                    for (int y = yi; y < yi + blockSize; y++)
                    {
                        for (int x = xi; x < xi + blockSize; x++)
                        {
                            int index = GetTerrainIndexRepeat(x, y);
                            float height = _backTerrainHeight[index].B * 1.5f;

                            minZ = Math.Min(minZ, height);
                            maxZ = Math.Max(maxZ, height);
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
            for (int i = 0; i < 4; i += lodi)
            {
                float temp = xf;
                for (int j = 0; j < 4; j += lodi)
                {
                    RenderTerrainTile(xf, yf, xi + j, yi + i, lodf, lodi, isAfter);
                    xf += lodf;
                }
                xf = temp;
                yf += lodf;
            }
        }
        private void RenderTerrainTile(float xf, float yf, int xi, int yi, float lodf, int lodi, bool isAfter)
        {
            if (isAfter) // we need check RenderTerrainFace_After
                return;

            var idx1 = GetTerrainIndex(xi, yi);

            if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.NoGround))
                return;

            var idx2 = GetTerrainIndex(xi + lodi, yi);
            var idx3 = GetTerrainIndex(xi + lodi, yi + lodi);
            var idx4 = GetTerrainIndex(xi, yi + lodi);

            var alpha1 = _mapping.Alpha[idx1];
            var alpha2 = _mapping.Alpha[idx2];
            var alpha3 = _mapping.Alpha[idx3];
            var alpha4 = _mapping.Alpha[idx4];

            var isOpaque = alpha1 >= 1 && alpha2 >= 1 && alpha3 >= 1 && alpha4 >= 1;
            var hasAlpha = alpha1 > 0 || alpha2 > 0 || alpha3 > 0 || alpha4 > 0;

            var terrainHeight1 = _backTerrainHeight[idx1].B * 1.5f;
            var terrainHeight2 = _backTerrainHeight[idx2].B * 1.5f;
            var terrainHeight3 = _backTerrainHeight[idx3].B * 1.5f;
            var terrainHeight4 = _backTerrainHeight[idx4].B * 1.5f;

            float sx = xf * Constants.TERRAIN_SCALE;
            float sy = yf * Constants.TERRAIN_SCALE;

            var terrainVertex = new Vector3[4]
            {
                new (sx, sy, terrainHeight1),
                new (sx + Constants.TERRAIN_SCALE, sy, terrainHeight2),
                new (sx + Constants.TERRAIN_SCALE, sy + Constants.TERRAIN_SCALE, terrainHeight3),
                new (sx, sy + Constants.TERRAIN_SCALE, terrainHeight4)
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

        private bool[] _notifiedNullTextures = new bool[256];
        private void RenderTexture(int textureIndex, float xf, float yf, Vector3[] terrainVertex, Color[] terrainLights)
        {
            if (_textures[textureIndex] == null)
            {
                if (!_notifiedNullTextures[textureIndex])
                {
                    _notifiedNullTextures[textureIndex] = true;
                    Debug.WriteLine($"Texture {textureIndex} is null for terrain {WorldIndex}");
                }
                return;
            }

            var texture = _textures[textureIndex];

            var width = 64f / texture.Width;
            var height = 64f / texture.Height;

            float suf = xf * width;
            float svf = yf * height;

            var terrainTextureCoord = new Vector2[4];
            terrainTextureCoord[0] = new Vector2(suf, svf);
            terrainTextureCoord[1] = new Vector2(suf + width, svf);
            terrainTextureCoord[2] = new Vector2(suf + width, svf + height);
            terrainTextureCoord[3] = new Vector2(suf, svf + height);

            var vertices2 = new VertexPositionColorTexture[6];
            vertices2[0] = new VertexPositionColorTexture(terrainVertex[0], terrainLights[0], terrainTextureCoord[0]);
            vertices2[1] = new VertexPositionColorTexture(terrainVertex[1], terrainLights[1], terrainTextureCoord[1]);
            vertices2[2] = new VertexPositionColorTexture(terrainVertex[2], terrainLights[2], terrainTextureCoord[2]);

            vertices2[3] = new VertexPositionColorTexture(terrainVertex[2], terrainLights[2], terrainTextureCoord[2]);
            vertices2[4] = new VertexPositionColorTexture(terrainVertex[3], terrainLights[3], terrainTextureCoord[3]);
            vertices2[5] = new VertexPositionColorTexture(terrainVertex[0], terrainLights[0], terrainTextureCoord[0]);

            _terrainEffect.Texture = texture;

            foreach (var pass in _terrainEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices2, 0, 2);
            }
        }

    }
}
