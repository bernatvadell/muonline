using Microsoft.Xna.Framework;
using System;

namespace Client.Main
{
        public static class Constants
        {
                // Terrain constants
                public const int TERRAIN_SIZE = 256;
                public const int TERRAIN_SIZE_MASK = 255;
                public const float TERRAIN_SCALE = 100f;

                // Game settings

#if DEBUG
                public static Type ENTRY_SCENE = typeof(Scenes.LoadScene);
                public static bool BACKGROUND_MUSIC = false;
                public static bool SOUND_EFFECTS = true;
                public static bool DRAW_BOUNDING_BOXES = false;
                public static bool DRAW_BOUNDING_BOXES_INTERACTIVES = false;
                public static bool DRAW_GRASS = true;
                public static bool ENABLE_LOW_QUALITY_SWITCH = true;
                public static bool ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = false;
                public static bool MSAA_ENABLED = false;
                /// <summary>
                /// Enables GPU-based dynamic lighting shader for 3D objects.
                /// When disabled, falls back to CPU-based lighting calculations.
                /// </summary>
                public static bool ENABLE_DYNAMIC_LIGHTING_SHADER = true;
                /// <summary>
                /// Reduces MAX_LIGHTS for integrated GPU performance optimization.
                /// When true, uses fewer lights but better performance on weak GPUs.
                /// </summary>
                public static bool OPTIMIZE_FOR_INTEGRATED_GPU = false;
                /// <summary>
                /// Debug mode that shows lighting areas as black spots for debugging light range.
                /// When enabled, areas affected by lights will appear as black patches on textures.
                /// </summary>
                public static bool DEBUG_LIGHTING_AREAS = false;
                /// <summary>
                /// Enables item material shader for items with level 7+, excellent, or ancient properties.
                /// When disabled, uses standard rendering for all items.
                /// </summary>
                public static bool ENABLE_ITEM_MATERIAL_SHADER = true;
                /// <summary>
                /// Enables monster material shader for custom monster effects.
                /// When disabled, uses standard rendering for all monsters.
                /// </summary>
                public static bool ENABLE_MONSTER_MATERIAL_SHADER = true;
                public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
                //public static string DataPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");
#else
                public static Type ENTRY_SCENE = typeof(Scenes.LoadScene);
                public static bool BACKGROUND_MUSIC = false;
                public static bool SOUND_EFFECTS = true;
                public static bool DRAW_BOUNDING_BOXES = false;
                public static bool DRAW_BOUNDING_BOXES_INTERACTIVES = false;
                public static bool DRAW_GRASS = true;
                public static bool ENABLE_LOW_QUALITY_SWITCH = true;
                public static bool ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = false;
                public static bool MSAA_ENABLED = false;
                /// <summary>
                /// Enables GPU-based dynamic lighting shader for 3D objects.
                /// When disabled, falls back to CPU-based lighting calculations.
                /// </summary>
                public static bool ENABLE_DYNAMIC_LIGHTING_SHADER = true;
                /// <summary>
                /// Reduces MAX_LIGHTS for integrated GPU performance optimization.
                /// When true, uses fewer lights but better performance on weak GPUs.
                /// </summary>
                public static bool OPTIMIZE_FOR_INTEGRATED_GPU = false;
                /// <summary>
                /// Debug mode that shows lighting areas as black spots for debugging light range.
                /// When enabled, areas affected by lights will appear as black patches on textures.
                /// </summary>
                public static bool DEBUG_LIGHTING_AREAS = false;
                /// <summary>
                /// Enables item material shader for items with level 7+, excellent, or ancient properties.
                /// When disabled, uses standard rendering for all items.
                /// </summary>
                public static bool ENABLE_ITEM_MATERIAL_SHADER = true;
                /// <summary>
                /// Enables monster material shader for custom monster effects.
                /// When disabled, uses standard rendering for all monsters.
                /// </summary>
                public static bool ENABLE_MONSTER_MATERIAL_SHADER = true;
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
                public const float MOVE_SPEED = 350f; // Default(?) walk speed

                // Others

                public const float BASE_FONT_SIZE = 25f;

                /// <summary>
                /// Distance after which objects are rendered in lower quality and
                /// dynamic lighting is disabled.
                /// </summary>
                public const float LOW_QUALITY_DISTANCE = 3500f;

                /// <summary>
                /// Enables drawing of object names when hovered with the mouse.
                /// </summary>
                public const bool SHOW_NAMES_ON_HOVER = true;

                // Android-specific adjustments
                /// <summary>
                /// Scale factor applied to the camera field of view on Android
                /// to reduce edge artifacts on wide screens.
                /// </summary>
                public const float ANDROID_FOV_SCALE = 0.8f;
        }
}
