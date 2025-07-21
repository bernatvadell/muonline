using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// Simulates wind movement for terrain effects like grass animation.
    /// </summary>
    public class WindSimulator
    {
        private const float WindScale = 15f;
        private const int UpdateIntervalMs = 32;

        private readonly TerrainData _data;
        private readonly WindCache _windCache = new();
        private float _lastWindSpeed = float.MinValue;
        private double _lastUpdateTime;

        public WindSimulator(TerrainData data)
        {
            _data = data;
        }

        public void Update(GameTime time)
        {
            if (_data.GrassWind == null) return;
            double nowMs = time.TotalGameTime.TotalMilliseconds;
            if (nowMs - _lastUpdateTime < UpdateIntervalMs) return;

            float windSpeed = (float)(nowMs % 720_000 * 0.002);
            if (Math.Abs(windSpeed - _lastWindSpeed) < 0.01f) return;

            _lastWindSpeed = windSpeed;
            _lastUpdateTime = nowMs;

            var cam = Camera.Instance;
            int cx = (int)(cam.Position.X / Constants.TERRAIN_SCALE);
            int cy = (int)(cam.Position.Y / Constants.TERRAIN_SCALE);
            int startX = Math.Max(0, cx - 32), startY = Math.Max(0, cy - 32);
            int endX = Math.Min(Constants.TERRAIN_SIZE - 1, startX + 64);
            int endY = Math.Min(Constants.TERRAIN_SIZE - 1, startY + 64);
            const float Step = 5f;

            Parallel.For(startY, endY + 1, y =>
            {
                int baseIdx = y * Constants.TERRAIN_SIZE;
                for (int x = startX; x <= endX; x++)
                {
                    _data.GrassWind[baseIdx + x] =
                        _windCache.FastSin(windSpeed + x * Step) * WindScale;
                }
            });
        }

        public float GetWindValue(int x, int y)
        {
            if (_data.GrassWind == null) return 0f;
            return _data.GrassWind[y * Constants.TERRAIN_SIZE + x];
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
    }
}
