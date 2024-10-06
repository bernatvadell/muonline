using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main
{
    public static class Constants
    {
        public const int TERRAIN_SIZE = 256;
        public const int TERRAIN_SIZE_MASK = 255;
        public const float TERRAIN_SCALE = 100f;

        public static bool DRAW_BOUNDING_BOXES = false;
        public static string DataPath = @"C:\Projects\MuMain\Source Main 5.2\bin\Data\";
        public static bool UNLIMITED_FPS = true;
    }
}
