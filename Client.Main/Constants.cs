using Microsoft.Xna.Framework;
using System;

namespace Client.Main
{
        public static class Constants
        {
                public static string IPAddress = "127.0.0.1";
                public static int Port = 44405;

                // Terrain constants
                public const int TERRAIN_SIZE = 256;
                public const int TERRAIN_SIZE_MASK = 255;
                public const float TERRAIN_SCALE = 100f;

                // Game settings

#if DEBUG
                public static Type ENTRY_SCENE = typeof(Scenes.LoadScene);
                public static bool BACKGROUND_MUSIC = true;
                public static bool SOUND_EFFECTS = true;
                public static bool DRAW_BOUNDING_BOXES = false;
                public static bool DRAW_BOUNDING_BOXES_INTERACTIVES = false;
                public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
                //public static string DataPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");
#else
                public static Type ENTRY_SCENE = typeof(Scenes.LoadScene);
                public static bool BACKGROUND_MUSIC = true;
                public static bool SOUND_EFFECTS = true;
                public static bool DRAW_BOUNDING_BOXES = false;
                public static bool DRAW_BOUNDING_BOXES_INTERACTIVES = false;
                public static string DataPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");
#endif
                public static string DataPathUrl = "http://192.168.55.220/Data.zip";
                public static string DefaultDataPathUrl = "https://full-wkr.mu.webzen.co.kr/muweb/full/MU_Red_1_20_61_Full.zip";
#if DEBUG
                public static bool UNLIMITED_FPS = true;
#else
                public static bool UNLIMITED_FPS = true;
#endif

                // Camera control constants
                public const float MIN_CAMERA_DISTANCE = 800f;
                public const float MAX_CAMERA_DISTANCE = 1800f;
                public const float ZOOM_SPEED = 4f;

                // Camera rotation constants
                public const float CAMERA_YAW = -0.7329271f; // Default(?) muonline view angle
                public static readonly float CAMERA_PITCH = 2.3711946f;
                public const float ROTATION_SENSITIVITY = 0.003f;

                // Default camera values
                public const float DEFAULT_CAMERA_DISTANCE = 1700f;
                public static readonly float DEFAULT_CAMERA_PITCH = 2.3711946f;
                public const float DEFAULT_CAMERA_YAW = -0.7329271f;

                // Rotation limits
                public static readonly float MAX_PITCH = MathHelper.ToRadians(160); // Limit upward rotation
                public static readonly float MIN_PITCH = MathHelper.ToRadians(110); // Limit downward rotation

                // Player movement speed
                public const float MOVE_SPEED = 400f; // Default(?) walk speed

                // Others

                public const float BASE_FONT_SIZE = 25f;
        }
}
