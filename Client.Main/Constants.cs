using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace Client.Main
{
    public static class Constants
    {
        // Terrain
        public const int TERRAIN_SIZE = 256;
        public const int TERRAIN_SIZE_MASK = 255;
        public const float TERRAIN_SCALE = 100f;

        // Camera control
        public const float MIN_CAMERA_DISTANCE = 800f;
        public const float MAX_CAMERA_DISTANCE = 1800f;
        public const float ZOOM_SPEED = 4f;

        // Camera rotation
        public static readonly float CAMERA_YAW = MathHelper.ToRadians(-41.99f);
        public static readonly float CAMERA_PITCH = MathHelper.ToRadians(135.87f);
        public const float ROTATION_SENSITIVITY = 0.003f;

        // Default camera values
        public const float DEFAULT_CAMERA_DISTANCE = 1700f;
        public static readonly float DEFAULT_CAMERA_PITCH = MathHelper.ToRadians(135.87f);
        public static readonly float DEFAULT_CAMERA_YAW = MathHelper.ToRadians(-41.99f);

        // Rotation limits
        public static readonly float MAX_PITCH = MathHelper.ToRadians(160);
        public static readonly float MIN_PITCH = MathHelper.ToRadians(110);

        // Player movement
        public const float MOVE_SPEED = 300f; // 12 * 25 FPS

        // UI base
        public const float BASE_FONT_SIZE = 25f;
        public const int BASE_UI_WIDTH = 1280;
        public const int BASE_UI_HEIGHT = 720;
        public const bool SHOW_NAMES_ON_HOVER = true;

        // Distance thresholds
        public const float LOW_QUALITY_DISTANCE = 3500f;

        // Scene / audio
        public static Type ENTRY_SCENE;
        public static bool BACKGROUND_MUSIC;
        public static bool SOUND_EFFECTS;
        public static float BACKGROUND_MUSIC_VOLUME;
        public static float SOUND_EFFECTS_VOLUME;

        // Debug / UI flags
        public static bool SHOW_DEBUG_PANEL;
        public static bool DRAW_BOUNDING_BOXES;
        public static bool DRAW_BOUNDING_BOXES_INTERACTIVES;
        public static bool ENABLE_LOW_QUALITY_SWITCH;
        public static bool ENABLE_LOW_QUALITY_IN_LOGIN_SCENE;

        // World visuals
        public static bool DRAW_GRASS;

        // Rendering
        public static bool MSAA_ENABLED;
        public static bool ENABLE_DYNAMIC_LIGHTS;
        public static bool ENABLE_DYNAMIC_LIGHTING_SHADER;
        public static bool ENABLE_TERRAIN_GPU_LIGHTING;
        public static bool OPTIMIZE_FOR_INTEGRATED_GPU;
        public static bool DEBUG_LIGHTING_AREAS;
        public static bool ENABLE_ITEM_MATERIAL_SHADER;
        public static bool ENABLE_MONSTER_MATERIAL_SHADER;
        public static bool ENABLE_WEAPON_TRAIL;
        public static bool ENABLE_BATCH_OPTIMIZED_SORTING;
        public static bool ENABLE_ITEM_MATERIAL_ANIMATION;
        public static bool ENABLE_DYNAMIC_BUFFER_POOL;
        public static float RENDER_SCALE;
        public static bool HIGH_QUALITY_TEXTURES;
        public static bool DISABLE_VSYNC;
        public static bool UNLIMITED_FPS;

        // Lighting
        public static bool SUN_ENABLED = true;
        public static Vector3 SUN_DIRECTION = new Vector3(-1f, 0f, -1f);
        public static float SUN_STRENGTH = 0.35f;
        public static float SUN_SHADOW_STRENGTH = 0.6f;
        public static short[] SUN_WORLD_INDICES = new short[] { 0, 2, 7, 1, 3 };
        public static bool ENABLE_SHADOW_MAPPING;
        public static int SHADOW_MAP_SIZE;
        public static float SHADOW_DISTANCE;
        public static int DYNAMIC_LIGHT_UPDATE_FPS;

        // Day-Night Cycle (real-time sun movement)
        public static bool ENABLE_DAY_NIGHT_CYCLE;
        public static float DAY_NIGHT_SPEED_MULTIPLIER = 1f; // 1 = real-time, 60 = 1 game day per real minute
        public static float SHADOW_NEAR_PLANE;
        public static float SHADOW_FAR_PLANE;
        public static float SHADOW_BIAS;
        public static float SHADOW_NORMAL_BIAS;
        public static int SHADOW_UPDATE_INTERVAL; // Frames between shadow map updates (1 = every frame)
        public static int SHADOW_MAX_CASTERS; // Max objects to render as shadow casters per frame
        public static bool SHADOW_SKIP_SMALL_PARTS; // Skip weapons/gloves/boots for shadow casting

        // Shadow quality presets
        public enum ShadowQuality
        {
            Off,
            Low,
            Medium,
            High,
            Ultra
        }

        public static void ApplyShadowQualityPreset(ShadowQuality quality)
        {
            switch (quality)
            {
                case ShadowQuality.Off:
                    ENABLE_SHADOW_MAPPING = false;
                    break;

                case ShadowQuality.Low:
                    ENABLE_SHADOW_MAPPING = true;
                    SHADOW_MAP_SIZE = 512;
                    SHADOW_DISTANCE = 1500f;
                    SHADOW_UPDATE_INTERVAL = 1;
                    SHADOW_MAX_CASTERS = 10;
                    SHADOW_SKIP_SMALL_PARTS = false;
                    break;

                case ShadowQuality.Medium:
                    ENABLE_SHADOW_MAPPING = true;
                    SHADOW_MAP_SIZE = 1024;
                    SHADOW_DISTANCE = 2000f;
                    SHADOW_UPDATE_INTERVAL = 1;
                    SHADOW_MAX_CASTERS = 15;
                    SHADOW_SKIP_SMALL_PARTS = false;
                    break;

                case ShadowQuality.High:
                    ENABLE_SHADOW_MAPPING = true;
                    SHADOW_MAP_SIZE = 1024;
                    SHADOW_DISTANCE = 3000f;
                    SHADOW_UPDATE_INTERVAL = 1;
                    SHADOW_MAX_CASTERS = 35;
                    SHADOW_SKIP_SMALL_PARTS = false;
                    break;

                case ShadowQuality.Ultra:
                    ENABLE_SHADOW_MAPPING = true;
                    SHADOW_MAP_SIZE = 2048;
                    SHADOW_DISTANCE = 4000f;
                    SHADOW_UPDATE_INTERVAL = 1;
                    SHADOW_MAX_CASTERS = 50;
                    SHADOW_SKIP_SMALL_PARTS = false;
                    break;
            }
        }

        public static ShadowQuality GetCurrentShadowQuality()
        {
            if (!ENABLE_SHADOW_MAPPING)
                return ShadowQuality.Off;

            // Match current settings to closest preset
            if (SHADOW_MAP_SIZE <= 512)
                return ShadowQuality.Low;
            if (SHADOW_MAP_SIZE <= 1024 && SHADOW_SKIP_SMALL_PARTS)
                return ShadowQuality.Medium;
            if (SHADOW_MAP_SIZE <= 1024)
                return ShadowQuality.High;
            return ShadowQuality.Ultra;
        }

        // Paths
        public static string DataPath;
        public static string DataPathUrl = "http://192.168.55.220/Data.zip";
        public static string DefaultDataPathUrl = "https://full-wkr.mu.webzen.co.kr/muweb/full/MU_Red_1_20_61_Full.zip";

        // Android-specific
        public const float ANDROID_FOV_SCALE = 0.8f;

        static Constants()
        {
            ApplyBaseDefaults();
#if DEBUG
            ApplyDebugDefaults();
#endif
#if ANDROID
            ApplyAndroidDefaults();
#endif
        }

        private static void ApplyBaseDefaults()
        {
            ENTRY_SCENE = typeof(Scenes.LoadScene);

            BACKGROUND_MUSIC = true;
            SOUND_EFFECTS = true;
            BACKGROUND_MUSIC_VOLUME = 50f;
            SOUND_EFFECTS_VOLUME = 100f;

            SHOW_DEBUG_PANEL = false;
            DRAW_BOUNDING_BOXES = false;
            DRAW_BOUNDING_BOXES_INTERACTIVES = false;
            DRAW_GRASS = true;
            ENABLE_LOW_QUALITY_SWITCH = true;
            ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = false;

            MSAA_ENABLED = false;
            ENABLE_DYNAMIC_LIGHTS = true;
            ENABLE_DYNAMIC_LIGHTING_SHADER = true;
            ENABLE_TERRAIN_GPU_LIGHTING = true;
            OPTIMIZE_FOR_INTEGRATED_GPU = false;
            DEBUG_LIGHTING_AREAS = false;
            ENABLE_ITEM_MATERIAL_SHADER = true;
            ENABLE_MONSTER_MATERIAL_SHADER = true;
            ENABLE_WEAPON_TRAIL = true;
            ENABLE_BATCH_OPTIMIZED_SORTING = true;
            ENABLE_ITEM_MATERIAL_ANIMATION = false;
            ENABLE_DYNAMIC_BUFFER_POOL = true;
            RENDER_SCALE = 2.0f;
            HIGH_QUALITY_TEXTURES = true;
            DISABLE_VSYNC = true;
            UNLIMITED_FPS = true;
            ENABLE_SHADOW_MAPPING = false;
            ENABLE_DAY_NIGHT_CYCLE = false;
            DAY_NIGHT_SPEED_MULTIPLIER = 60f;
            DYNAMIC_LIGHT_UPDATE_FPS = 30;
            // Default to the Medium preset unless user changes it in options
            // ApplyShadowQualityPreset(ShadowQuality.Medium);
            SHADOW_NEAR_PLANE = 10f;
            SHADOW_FAR_PLANE = 6000f;
            SHADOW_BIAS = 0.005f;
            SHADOW_NORMAL_BIAS = 0.008f;

            DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        }

#if DEBUG
        private static void ApplyDebugDefaults()
        {
            BACKGROUND_MUSIC = false;
            SHOW_DEBUG_PANEL = true;

#if WINDOWS
            DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
#endif
        }
#endif

#if ANDROID
        private static void ApplyAndroidDefaults()
        {
            DRAW_GRASS = false;
            ENABLE_DYNAMIC_LIGHTS = false;
            ENABLE_DYNAMIC_LIGHTING_SHADER = true;
            ENABLE_TERRAIN_GPU_LIGHTING = false;
            OPTIMIZE_FOR_INTEGRATED_GPU = true;
            ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = true;
            ENABLE_ITEM_MATERIAL_SHADER = true;
            ENABLE_MONSTER_MATERIAL_SHADER = true;
            ENABLE_WEAPON_TRAIL = false;
            HIGH_QUALITY_TEXTURES = false;
            RENDER_SCALE = 0.75f;
            DYNAMIC_LIGHT_UPDATE_FPS = 30;
        }
#endif
    }
}
