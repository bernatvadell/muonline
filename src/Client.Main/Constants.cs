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

        public const Models.PlayerClass Character = Models.PlayerClass.DarkWizard;

        // Game settings
        public static Type ENTRY_SCENE = typeof(Scenes.GameScene);
        public static bool BACKGROUND_MUSIC = false;
        public static bool SOUND_EFFECTS = false;
        public static bool DRAW_BOUNDING_BOXES = false;
        public static bool DRAW_BOUNDING_BOXES_INTERACTIVES = false;

#if DEBUG
        public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
#else
        public static string DataPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");
#endif

#if DEBUG
        public static bool UNLIMITED_FPS = true;
#else
        public static bool UNLIMITED_FPS = false;
#endif

        // Camera control constants
        public const float MIN_CAMERA_DISTANCE = 800f;
        public const float MAX_CAMERA_DISTANCE = 1800f;
        public const float ZOOM_SPEED = 4f;

        // Camera rotation constants
        public const float CAMERA_YAW = 30.5f; // Default(?) muonline view angle
        public static readonly float CAMERA_PITCH = MathHelper.ToRadians(135);
        public const float ROTATION_SENSITIVITY = 0.003f;

        // Default camera values
        public const float DEFAULT_CAMERA_DISTANCE = 1500f;
        public static readonly float DEFAULT_CAMERA_PITCH = MathHelper.ToRadians(135);
        public const float DEFAULT_CAMERA_YAW = 30.5f;

        // Rotation limits
        public static readonly float MAX_PITCH = MathHelper.ToRadians(0); // Limit upward rotation
        public static readonly float MIN_PITCH = MathHelper.ToRadians(135); // Limit downward rotation

        // Player movement speed
        public const float MOVE_SPEED = 250f; // Default(?) walk speed
    }
}
