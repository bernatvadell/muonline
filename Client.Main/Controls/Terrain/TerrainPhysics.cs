using Client.Data.ATT;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Provides methods to query terrain properties like height, flags, and lighting.
    /// </summary>
    public class TerrainPhysics
    {
        private const float SpecialHeight = 1200f;

        private readonly TerrainData _data;
        private readonly TerrainLightManager _lightManager;

        public TerrainPhysics(TerrainData data, TerrainLightManager lightManager)
        {
            _data = data;
            _lightManager = lightManager;
        }

        public TWFlags RequestTerrainFlag(int x, int y)
        {
            if (_data.Attributes == null)
                return default;

            return _data.Attributes.TerrainWall[GetTerrainIndex(x, y)];
        }

        public float RequestTerrainHeight(float xf, float yf)
        {
            if (_data.Attributes?.TerrainWall == null
                || xf < 0 || yf < 0
                || _data.HeightMap == null
                || float.IsNaN(xf) || float.IsNaN(yf))
                return 0f;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf, yi = (int)yf;
            int index = GetTerrainIndex(xi, yi);

            if (index < _data.Attributes.TerrainWall.Length
                && _data.Attributes.TerrainWall[index].HasFlag(TWFlags.Height))
                return SpecialHeight;

            float xd = xf - xi, yd = yf - yi;
            int x1 = xi & Constants.TERRAIN_SIZE_MASK, y1 = yi & Constants.TERRAIN_SIZE_MASK;
            int x2 = (xi + 1) & Constants.TERRAIN_SIZE_MASK, y2 = (yi + 1) & Constants.TERRAIN_SIZE_MASK;

            int i1 = y1 * Constants.TERRAIN_SIZE + x1;
            int i2 = y1 * Constants.TERRAIN_SIZE + x2;
            int i3 = y2 * Constants.TERRAIN_SIZE + x2;
            int i4 = y2 * Constants.TERRAIN_SIZE + x1;

            float h1 = _data.HeightMap[i1].B;
            float h2 = _data.HeightMap[i2].B;
            float h3 = _data.HeightMap[i3].B;
            float h4 = _data.HeightMap[i4].B;

            return (1 - xd) * (1 - yd) * h1
                 + xd * (1 - yd) * h2
                 + xd * yd * h3
                 + (1 - xd) * yd * h4;
        }

        public Vector3 RequestTerrainLight(float xf, float yf, float ambientLight)
        {
            if (_data.Attributes?.TerrainWall == null
                || xf < 0 || yf < 0
                || _data.FinalLightMap == null)
                return Vector3.One;

            xf /= Constants.TERRAIN_SCALE;
            yf /= Constants.TERRAIN_SCALE;

            int xi = (int)xf, yi = (int)yf;
            float xd = xf - xi, yd = yf - yi;

            int i1 = xi + yi * Constants.TERRAIN_SIZE;
            int i2 = (xi + 1) + yi * Constants.TERRAIN_SIZE;
            int i3 = (xi + 1) + (yi + 1) * Constants.TERRAIN_SIZE;
            int i4 = xi + (yi + 1) * Constants.TERRAIN_SIZE;

            if (new[] { i1, i2, i3, i4 }.Any(i => i < 0 || i >= _data.FinalLightMap.Length))
                return Vector3.Zero;

            float[] rgb = new float[3];
            for (int c = 0; c < 3; c++)
            {
                float left = MathHelper.Lerp(GetChannel(_data.FinalLightMap[i1], c),
                                              GetChannel(_data.FinalLightMap[i4], c),
                                              yd);
                float right = MathHelper.Lerp(GetChannel(_data.FinalLightMap[i2], c),
                                              GetChannel(_data.FinalLightMap[i3], c),
                                              yd);
                rgb[c] = MathHelper.Lerp(left, right, xd);
            }

            var result = new Vector3(rgb[0], rgb[1], rgb[2])
                       + new Vector3(ambientLight * 255f)
                       + _lightManager.EvaluateDynamicLight(new Vector2(xf * Constants.TERRAIN_SCALE, yf * Constants.TERRAIN_SCALE));
            result = Vector3.Clamp(result, Vector3.Zero, new Vector3(255f));
            return result / 255f;
        }

        public byte GetBaseTextureIndexAt(int x, int y)
        {
            if (_data.Mapping.Layer1 == null || _data.Mapping.Layer2 == null || _data.Mapping.Alpha == null)
                return 0;

            x = Math.Clamp(x, 0, Constants.TERRAIN_SIZE - 1);
            y = Math.Clamp(y, 0, Constants.TERRAIN_SIZE - 1);
            int idx = GetTerrainIndex(x, y);
            byte alpha = _data.Mapping.Alpha[idx];
            return alpha == 255 ? _data.Mapping.Layer2[idx] : _data.Mapping.Layer1[idx];
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

        private static int GetTerrainIndex(int x, int y)
            => y * Constants.TERRAIN_SIZE + x;
    }
}
