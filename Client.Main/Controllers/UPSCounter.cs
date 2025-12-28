using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controllers
{
    public class UPSCounter
    {
        public static UPSCounter Instance { get; private set; } = new UPSCounter();

        private bool _timeInit = false;
        private double _startTime = 0;
        private double _lastTime = 0;
        private int _frameCount = 0;

        public double WorldTime { get; private set; }
        public double UPS { get; private set; }
        public double UPS_AVG { get; private set; }


        public void CalcUPS(GameTime gameTime)
        {
            if (!_timeInit)
            {
                _startTime = gameTime.TotalGameTime.TotalMilliseconds;
                _timeInit = true;
            }

            _frameCount++;
            WorldTime = gameTime.TotalGameTime.TotalMilliseconds;

            double differenceMs = WorldTime - _lastTime;
            if (differenceMs <= 0)
            {
                UPS = 0.01;
            }
            else
            {
                UPS = 1000 / differenceMs;
            }

            double diffSinceStart = WorldTime - _startTime;
            if (diffSinceStart > 2000.0 || _frameCount > 25)
            {
                UPS_AVG = (1000.0 * _frameCount) / diffSinceStart;
                _startTime = WorldTime;
                _frameCount = 0;
            }

            _lastTime = WorldTime;
        }
    }

}
