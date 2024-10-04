using Client.Data.ATT;
using Client.Data.BMD;
using Client.Data.CWS;
using Client.Data.MAP;
using Client.Data.OBJS;
using Client.Data.OZB;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private TextureData _terrainLightTexture;
        private Vector3[] _terrainLight;
        private Vector3[] _backTerrainLight;
        private Vector3[] _terrainNormal;
        private byte[] _backTerrainHeight;
        private GraphicsDevice _graphicsDevice;

        public short WorldIndex { get; set; }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;

            var terrainReader = new ATTReader();
            var ozbReader = new OZBReader();
            var objReader = new OBJReader();
            var mapping = new MapReader();
            var bmdReader = new BMDReader();

            var tasks = new List<Task>();
            var worldFolder = $"World{WorldIndex}";

            Camera.Instance.AspectRatio = graphicsDevice.Viewport.AspectRatio;

            _terrainEffect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                World = Matrix.Identity
            };


            tasks.Add(terrainReader.Load(Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.att")).ContinueWith(t => _terrain = t.Result));
            tasks.Add(ozbReader.Load(Path.Combine(Constants.DataPath, worldFolder, $"TerrainHeight.OZB")).ContinueWith(t => _backTerrainHeight = t.Result.BackTerrainHeight));
            tasks.Add(mapping.Load(Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.map")).ContinueWith(t => _mapping = t.Result));

            var textureMapFiles = new string[255];
            textureMapFiles[0] = Path.Combine(Constants.DataPath, worldFolder, "TileGrass01.ozj");
            textureMapFiles[1] = Path.Combine(Constants.DataPath, worldFolder, "TileGrass02.ozj");
            textureMapFiles[2] = Path.Combine(Constants.DataPath, worldFolder, "TileGround01.ozj");
            textureMapFiles[3] = Path.Combine(Constants.DataPath, worldFolder, "TileGround02.ozj");
            textureMapFiles[4] = Path.Combine(Constants.DataPath, worldFolder, "TileGround03.ozj");
            textureMapFiles[5] = Path.Combine(Constants.DataPath, worldFolder, "TileWater01.ozj");
            textureMapFiles[6] = Path.Combine(Constants.DataPath, worldFolder, "TileWood01.ozj");
            textureMapFiles[7] = Path.Combine(Constants.DataPath, worldFolder, "TileRock01.ozj");
            textureMapFiles[8] = Path.Combine(Constants.DataPath, worldFolder, "TileRock02.ozj");
            textureMapFiles[9] = Path.Combine(Constants.DataPath, worldFolder, "TileRock03.ozj");
            textureMapFiles[10] = Path.Combine(Constants.DataPath, worldFolder, "AlphaTile01.Tga");
            textureMapFiles[11] = Path.Combine(Constants.DataPath, worldFolder, "TileRock05.ozj");
            textureMapFiles[12] = Path.Combine(Constants.DataPath, worldFolder, "TileRock06.ozj");
            textureMapFiles[13] = Path.Combine(Constants.DataPath, worldFolder, "TileRock07.ozj");

            for (int i = 1; i <= 16; i++)
                textureMapFiles[13 + i] = Path.Combine(Constants.DataPath, worldFolder, $"ExtTile{i.ToString().PadLeft(2, '0')}.ozj");

            textureMapFiles[13] = Path.Combine(Constants.DataPath, worldFolder, "TileRock07.ozj");

            textureMapFiles[30] = Path.Combine(Constants.DataPath, worldFolder, "TileGrass01.tga");
            textureMapFiles[31] = Path.Combine(Constants.DataPath, worldFolder, "TileGrass02.tga");
            textureMapFiles[32] = Path.Combine(Constants.DataPath, worldFolder, "TileGrass03.tga");

            textureMapFiles[100] = Path.Combine(Constants.DataPath, worldFolder, "leaf01.jpg");
            textureMapFiles[101] = Path.Combine(Constants.DataPath, worldFolder, "leaf02.jpg");

            textureMapFiles[102] = Path.Combine(Constants.DataPath, "World1", "rain01.tga");
            textureMapFiles[103] = Path.Combine(Constants.DataPath, "World1", "rain02.tga");
            textureMapFiles[104] = Path.Combine(Constants.DataPath, "World1", "rain03.tga");

            _textures = new Texture2D[textureMapFiles.Length];

            for (int t = 0; t < textureMapFiles.Length; t++)
            {
                var path = textureMapFiles[t];
                if (!File.Exists(path)) continue;
                var tt = t;
                tasks.Add(TextureLoader.Instance.Prepare(path).ContinueWith((_) => _textures[tt] = TextureLoader.Instance.GetTexture2D(path)));
            }

            var textureLightPath = Path.Combine(worldFolder, "TerrainLight.jpg");
            tasks.Add(TextureLoader.Instance.Prepare(textureLightPath).ContinueWith((_) => _terrainLightTexture = TextureLoader.Instance.Get(textureLightPath)));

            await Task.WhenAll(tasks);

            CreateTerrainNormal();
            CreateTerrainLight();

            await base.Load(graphicsDevice);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            _terrainEffect.Projection = Camera.Instance.Projection;
            _terrainEffect.View = Camera.Instance.View;

            InitTerrainLight(time);
        }

        public override void Draw(GameTime time)
        {
            RenderTerrain();

            base.Draw(time);
        }

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

            float left = _backTerrainHeight[Index1] + (_backTerrainHeight[Index2] - _backTerrainHeight[Index1]) * yd;
            float right = _backTerrainHeight[Index3] + (_backTerrainHeight[Index4] - _backTerrainHeight[Index3]) * yd;
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

            int Index1 = GetTerrainIndexRepeat(xi, yi);
            int Index2 = GetTerrainIndexRepeat(xi, yi + 1);
            int Index3 = GetTerrainIndexRepeat(xi + 1, yi);
            int Index4 = GetTerrainIndexRepeat(xi + 1, yi + 1);

            if (Index1 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || Index2 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE ||
                    Index3 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE || Index4 >= Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE)
                return Vector3.Zero;

            var output = new float[3];

            for (var i = 0; i < 3; i++)
            {
                float left = 0;
                float right = 0;

                switch (i)
                {
                    case 0:
                        left = _backTerrainLight[Index1].X + (_backTerrainLight[Index4].X - _backTerrainLight[Index1].X) * yd;
                        right = _backTerrainLight[Index1].X + (_backTerrainLight[Index4].X - _backTerrainLight[Index1].X) * yd;
                        break;
                    case 1:
                        left = _backTerrainLight[Index1].Y + (_backTerrainLight[Index4].Y - _backTerrainLight[Index1].Y) * yd;
                        right = _backTerrainLight[Index1].Y + (_backTerrainLight[Index4].Y - _backTerrainLight[Index1].Y) * yd;
                        break;
                    case 2:
                        left = _backTerrainLight[Index1].Z + (_backTerrainLight[Index4].Z - _backTerrainLight[Index1].Z) * yd;
                        right = _backTerrainLight[Index1].Z + (_backTerrainLight[Index4].Z - _backTerrainLight[Index1].Z) * yd;
                        break;
                }

                output[i] = (left + (right - left) * xd);
            }

            return new Vector3(output[0], output[1], output[2]);
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

                    v4 = new Vector3(x * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x, y)]);
                    v1 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, y * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y)]);
                    v2 = new Vector3((x + 1) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x + 1, y + 1)]);
                    v3 = new Vector3((x) * Constants.TERRAIN_SCALE, (y + 1) * Constants.TERRAIN_SCALE, _backTerrainHeight[GetTerrainIndexRepeat(x, y + 1)]);

                    Vector3 faceNormal = MathUtils.FaceNormalize(v1, v2, v3);
                    _terrainNormal[Index] = _terrainNormal[Index] + faceNormal;
                    faceNormal = MathUtils.FaceNormalize(v3, v4, v1);
                    _terrainNormal[Index] = _terrainNormal[Index] + faceNormal;
                }
            }
        }

        private void CreateTerrainLight()
        {
            var lightTextureInfo = _terrainLightTexture;

            _terrainLight = new Vector3[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
            _backTerrainLight = new Vector3[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];

            for (var i = 0; i < lightTextureInfo.Data.Length; i += 3)
                _terrainLight[i / 3] = new Vector3(lightTextureInfo.Data[i] / 255f, lightTextureInfo.Data[i + 1] / 255f, lightTextureInfo.Data[i + 2] / 255f);

            var _light = new Vector3(0.5f, 0.5f, 0.5f);
            for (int y = 0; y < Constants.TERRAIN_SIZE; y++)
            {
                for (int x = 0; x < Constants.TERRAIN_SIZE; x++)
                {
                    int Index = GetTerrainIndex(x, y);
                    float Luminosity = MathUtils.DotProduct(_terrainNormal[Index], _light) + 0.5f;
                    if (Luminosity < 0f)
                        Luminosity = 0f;
                    else if (Luminosity > 1f)
                        Luminosity = 1f;

                    _backTerrainLight[Index] = _terrainLight[Index] * Luminosity;
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

        private void RenderTerrain()
        {
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
                            float height = _backTerrainHeight[index] * 1.5f;

                            minZ = Math.Min(minZ, height);
                            maxZ = Math.Max(maxZ, height);
                        }
                    }

                    BoundingBox blockBounds = new BoundingBox(
                        new Vector3(xStart, yStart, minZ),
                        new Vector3(xEnd, yEnd, maxZ)
                    );

                    if (Camera.Instance.Frustum.Intersects(blockBounds))
                        RenderTerrainBlock(xStart / Constants.TERRAIN_SCALE, yStart / Constants.TERRAIN_SCALE, xi, yi);
                }
            }
        }


        private void RenderTerrainBlock(float xf, float yf, int xi, int yi)
        {
            int lodi = 1;
            var lodf = (float)lodi;
            for (int i = 0; i < 4; i += lodi)
            {
                float temp = xf;
                for (int j = 0; j < 4; j += lodi)
                {
                    RenderTerrainTile(xf, yf, xi + j, yi + i, lodf, lodi);
                    xf += lodf;
                }
                xf = temp;
                yf += lodf;
            }
        }

        private void RenderTerrainTile(float xf, float yf, int xi, int yi, float lodf, int lodi)
        {
            var idx1 = GetTerrainIndex(xi, yi);

            if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.NoGround))
                return;

            var idx2 = GetTerrainIndex(xi + lodi, yi);
            var idx3 = GetTerrainIndex(xi + lodi, yi + lodi);
            var idx4 = GetTerrainIndex(xi, yi + lodi);



            //var alpha1 = _mapping.Alpha[idx1];
            //var alpha2 = _mapping.Alpha[idx2];
            //var alpha3 = _mapping.Alpha[idx3];
            //var alpha4 = _mapping.Alpha[idx4];

            //if (alpha1 == 0 && alpha2 == 0 && alpha3 == 0 && alpha4 == 0)
            //    return;

            var terrainHeight1 = _backTerrainHeight[idx1] * 1.5f;
            var terrainHeight2 = _backTerrainHeight[idx2] * 1.5f;
            var terrainHeight3 = _backTerrainHeight[idx3] * 1.5f;
            var terrainHeight4 = _backTerrainHeight[idx4] * 1.5f;

            float sx = xf * Constants.TERRAIN_SCALE;
            float sy = yf * Constants.TERRAIN_SCALE;

            var terrainVertex = new Vector3[4];
            terrainVertex[0] = new Vector3(sx, sy, terrainHeight1);
            terrainVertex[1] = new Vector3(sx + Constants.TERRAIN_SCALE, sy, terrainHeight2);
            terrainVertex[2] = new Vector3(sx + Constants.TERRAIN_SCALE, sy + Constants.TERRAIN_SCALE, terrainHeight3);
            terrainVertex[3] = new Vector3(sx, sy + Constants.TERRAIN_SCALE, terrainHeight4);

            if (_terrain.TerrainWall[idx1].HasFlag(TWFlags.Height))
                terrainVertex[0].Z += 1200f;

            if (_terrain.TerrainWall[idx2].HasFlag(TWFlags.Height))
                terrainVertex[1].Z += 1200f;

            if (_terrain.TerrainWall[idx3].HasFlag(TWFlags.Height))
                terrainVertex[2].Z += 1200f;

            if (_terrain.TerrainWall[idx4].HasFlag(TWFlags.Height))
                terrainVertex[3].Z += 1200f;

            var terrainLights = new Vector3[4];
            terrainLights[0] = _backTerrainLight[idx1];
            terrainLights[1] = _backTerrainLight[idx2];
            terrainLights[2] = _backTerrainLight[idx3];
            terrainLights[3] = _backTerrainLight[idx4];

            RenderTexture(_mapping.Layer1[idx1], xf, yf, terrainVertex, terrainLights);

            // RenderTexture(_mapping.Layer2[idx1], xf, yf, terrainVertex, 255);

            /*if (alpha1 > 0)
                RenderTexture(_mapping.Layer1[idx1], xf, yf, terrainVertex, alpha1);

            if (alpha2 > 0)
                RenderTexture(_mapping.Layer2[idx1], xf, yf, terrainVertex, alpha2);*/

            // RenderTexture(alpha ? _mapping.Layer1[idx1] : _mapping.Layer2[idx1], xf, yf, terrainVertex, alpha1);
            // if (alpha) RenderTexture(alpha ? _mapping.Layer2[idx1] : _mapping.Layer1[idx1], xf, yf, terrainVertex, alpha1);
        }

        private void RenderTexture(int textureIndex, float xf, float yf, Vector3[] terrainVertex, Vector3[] terrainLights)
        {
            if (textureIndex == 255 || _textures[textureIndex] == null)
                return;

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
            vertices2[0] = new VertexPositionColorTexture(terrainVertex[0], new Color(terrainLights[0].X, terrainLights[0].Y, terrainLights[0].Z), terrainTextureCoord[0]);
            vertices2[1] = new VertexPositionColorTexture(terrainVertex[1], new Color(terrainLights[1].X, terrainLights[1].Y, terrainLights[1].Z), terrainTextureCoord[1]);
            vertices2[2] = new VertexPositionColorTexture(terrainVertex[2], new Color(terrainLights[2].X, terrainLights[2].Y, terrainLights[2].Z), terrainTextureCoord[2]);

            vertices2[3] = new VertexPositionColorTexture(terrainVertex[2], new Color(terrainLights[2].X, terrainLights[2].Y, terrainLights[2].Z), terrainTextureCoord[2]);
            vertices2[4] = new VertexPositionColorTexture(terrainVertex[3], new Color(terrainLights[3].X, terrainLights[3].Y, terrainLights[3].Z), terrainTextureCoord[3]);
            vertices2[5] = new VertexPositionColorTexture(terrainVertex[0], new Color(terrainLights[0].X, terrainLights[0].Y, terrainLights[0].Z), terrainTextureCoord[0]);

            _terrainEffect.Texture = texture;

            foreach (var pass in _terrainEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices2, 0, 2);
            }
        }

    }
}
