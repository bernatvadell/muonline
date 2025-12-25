using System;
using System.Reflection;
using Client.Main.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Graphics
{
    public enum GraphicsQualityPreset
    {
        Auto,
        Low,
        Medium,
        High
    }

    public readonly struct GraphicsAdapterInfo
    {
        public string Description { get; }
        public string DeviceName { get; }
        public int? VendorId { get; }
        public int? DeviceId { get; }
        public bool IsIntegrated { get; }
        public bool IsDiscrete { get; }
        public bool IsSoftware { get; }

        public GraphicsAdapterInfo(
            string description,
            string deviceName,
            int? vendorId,
            int? deviceId,
            bool isIntegrated,
            bool isDiscrete,
            bool isSoftware)
        {
            Description = description ?? string.Empty;
            DeviceName = deviceName ?? string.Empty;
            VendorId = vendorId;
            DeviceId = deviceId;
            IsIntegrated = isIntegrated;
            IsDiscrete = isDiscrete;
            IsSoftware = isSoftware;
        }

        public override string ToString()
        {
            string vendor = VendorId.HasValue ? $"0x{VendorId.Value:X4}" : "unknown";
            return $"{Description} ({DeviceName}) Vendor={vendor}, Integrated={IsIntegrated}, Discrete={IsDiscrete}, Software={IsSoftware}";
        }
    }

    public static class GraphicsQualityManager
    {
        public static GraphicsQualityPreset UserPreset { get; private set; } = GraphicsQualityPreset.Auto;
        public static GraphicsQualityPreset ActivePreset { get; private set; } = GraphicsQualityPreset.High;
        public static GraphicsAdapterInfo LastAdapterInfo { get; private set; } = new GraphicsAdapterInfo(string.Empty, string.Empty, null, null, false, false, false);

        public static GraphicsQualityPreset ParsePreset(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GraphicsQualityPreset.Auto;

            if (Enum.TryParse(value, true, out GraphicsQualityPreset preset))
                return preset;

            return GraphicsQualityPreset.Auto;
        }

        public static void ApplyFromSettings(GraphicsSettings settings, GraphicsAdapter adapter, ILogger logger)
        {
            var preset = ParsePreset(settings?.QualityPreset);
            ApplyPreset(preset, adapter, logger);
        }

        public static void ApplyPreset(GraphicsQualityPreset preset, GraphicsAdapter adapter, ILogger logger)
        {
            UserPreset = preset;
            var resolved = ResolvePreset(preset, adapter);
            ActivePreset = resolved;
            ApplyProfile(resolved);

            if (logger != null)
            {
                if (preset == GraphicsQualityPreset.Auto)
                {
                    logger.LogInformation("Graphics preset: {UserPreset} -> {ResolvedPreset}. Adapter: {Adapter}",
                        preset, resolved, LastAdapterInfo.ToString());
                }
                else
                {
                    logger.LogInformation("Graphics preset: {UserPreset} applied.", preset);
                }
            }
        }

        private static GraphicsQualityPreset ResolvePreset(GraphicsQualityPreset preset, GraphicsAdapter adapter)
        {
            if (preset != GraphicsQualityPreset.Auto)
                return preset;

#if ANDROID
            return GraphicsQualityPreset.Low;
#else
            LastAdapterInfo = GetAdapterInfo(adapter);

            if (LastAdapterInfo.IsSoftware || LastAdapterInfo.IsIntegrated)
                return GraphicsQualityPreset.Medium;

            if (LastAdapterInfo.IsDiscrete)
                return GraphicsQualityPreset.High;

            // Unknown adapters default to Medium for safety.
            return GraphicsQualityPreset.Medium;
#endif
        }

        private static void ApplyProfile(GraphicsQualityPreset preset)
        {
            switch (preset)
            {
                case GraphicsQualityPreset.Low:
                    Constants.RENDER_SCALE = 0.75f;
                    Constants.MSAA_ENABLED = false;
                    Constants.ENABLE_DYNAMIC_LIGHTS = false;
                    Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = false;
                    Constants.ENABLE_TERRAIN_GPU_LIGHTING = false;
                    Constants.OPTIMIZE_FOR_INTEGRATED_GPU = true;
                    Constants.HIGH_QUALITY_TEXTURES = false;
                    Constants.DRAW_GRASS = false;
                    Constants.ENABLE_ITEM_MATERIAL_SHADER = false;
                    Constants.ENABLE_MONSTER_MATERIAL_SHADER = false;
                    Constants.ENABLE_WEAPON_TRAIL = false;
                    Constants.DYNAMIC_LIGHT_UPDATE_FPS = 20;
                    break;

                case GraphicsQualityPreset.Medium:
                    Constants.RENDER_SCALE = 1.0f;
                    Constants.MSAA_ENABLED = false;
                    Constants.ENABLE_DYNAMIC_LIGHTS = true;
                    Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = true;
                    Constants.ENABLE_TERRAIN_GPU_LIGHTING = false;
                    Constants.OPTIMIZE_FOR_INTEGRATED_GPU = false;
                    Constants.HIGH_QUALITY_TEXTURES = true;
                    Constants.DRAW_GRASS = false;
                    Constants.ENABLE_ITEM_MATERIAL_SHADER = true;
                    Constants.ENABLE_MONSTER_MATERIAL_SHADER = true;
                    Constants.ENABLE_WEAPON_TRAIL = true;
                    Constants.DYNAMIC_LIGHT_UPDATE_FPS = 30;
                    break;

                case GraphicsQualityPreset.High:
                default:
                    Constants.RENDER_SCALE = 1.0f;
                    Constants.MSAA_ENABLED = false;
                    Constants.ENABLE_DYNAMIC_LIGHTS = true;
                    Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = true;
                    Constants.ENABLE_TERRAIN_GPU_LIGHTING = true;
                    Constants.OPTIMIZE_FOR_INTEGRATED_GPU = false;
                    Constants.HIGH_QUALITY_TEXTURES = true;
                    Constants.DRAW_GRASS = true;
                    Constants.ENABLE_ITEM_MATERIAL_SHADER = true;
                    Constants.ENABLE_MONSTER_MATERIAL_SHADER = true;
                    Constants.ENABLE_WEAPON_TRAIL = true;
                    Constants.DYNAMIC_LIGHT_UPDATE_FPS = 30;
                    break;
            }

            // Keep terrain GPU lighting consistent with shader usage.
            if (!Constants.ENABLE_DYNAMIC_LIGHTING_SHADER)
            {
                Constants.ENABLE_TERRAIN_GPU_LIGHTING = false;
            }
        }

        private static GraphicsAdapterInfo GetAdapterInfo(GraphicsAdapter adapter)
        {
            string description = adapter?.Description ?? string.Empty;
            string deviceName = TryGetAdapterString(adapter, "DeviceName");

            int? vendorId = TryGetAdapterInt(adapter, "VendorId");
            int? deviceId = TryGetAdapterInt(adapter, "DeviceId");

            string name = $"{description} {deviceName}".ToLowerInvariant();

            bool isSoftware = name.Contains("microsoft basic render") ||
                              name.Contains("swiftshader") ||
                              vendorId == 0x1414;

            bool isIntel = vendorId == 0x8086 || name.Contains("intel");
            bool isNvidia = vendorId == 0x10DE || name.Contains("nvidia") || name.Contains("geforce");
            bool isAmd = vendorId == 0x1002 || vendorId == 0x1022 || name.Contains("amd") || name.Contains("radeon");

            bool isAmdIntegrated = isAmd &&
                                   (name.Contains("apu") ||
                                    name.Contains("ryzen") ||
                                    name.Contains("athlon") ||
                                    name.Contains("radeon(tm) graphics") ||
                                    name.Contains("vega") ||
                                    name.Contains("embedded"));

            bool isIntegrated = isIntel ||
                                isAmdIntegrated ||
                                name.Contains("uhd") ||
                                name.Contains("iris") ||
                                name.Contains("xe graphics") ||
                                name.Contains("integrated");

            bool isDiscrete = isNvidia || (isAmd && !isAmdIntegrated);

            return new GraphicsAdapterInfo(description, deviceName, vendorId, deviceId, isIntegrated, isDiscrete, isSoftware);
        }

        private static int? TryGetAdapterInt(GraphicsAdapter adapter, string propertyName)
        {
            if (adapter == null)
                return null;

            try
            {
                var prop = adapter.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || prop.PropertyType != typeof(int))
                    return null;

                return (int)prop.GetValue(adapter);
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetAdapterString(GraphicsAdapter adapter, string propertyName)
        {
            if (adapter == null)
                return string.Empty;

            try
            {
                var prop = adapter.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || prop.PropertyType != typeof(string))
                    return string.Empty;

                return (string)prop.GetValue(adapter) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
