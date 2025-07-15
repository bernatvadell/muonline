using Microsoft.Xna.Framework;
using Client.Main.Objects;

namespace Client.Main.Extensions
{
    public static class ModelObjectExtensions
    {
        /// <summary>
        /// Sets glow properties for monsters/NPCs with custom shader
        /// </summary>
        public static void SetGlow(this ModelObject obj, Vector3 color, float intensity)
        {
            obj.GlowColor = color;
            obj.GlowIntensity = intensity;
            obj.EnableCustomShader = intensity > 0.0f;
        }

        /// <summary>
        /// Disables glow effect
        /// </summary>
        public static void DisableGlow(this ModelObject obj)
        {
            obj.EnableCustomShader = false;
            obj.GlowIntensity = 0.0f;
        }

        /// <summary>
        /// Sets gold glow effect
        /// </summary>
        public static void SetGoldGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(1.0f, 0.8f, 0.0f), intensity);
        }

        /// <summary>
        /// Sets red glow effect
        /// </summary>
        public static void SetRedGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(1.0f, 0.2f, 0.2f), intensity);
        }

        /// <summary>
        /// Sets blue glow effect
        /// </summary>
        public static void SetBlueGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(0.2f, 0.4f, 1.0f), intensity);
        }

        /// <summary>
        /// Sets green glow effect
        /// </summary>
        public static void SetGreenGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(0.2f, 1.0f, 0.2f), intensity);
        }

        /// <summary>
        /// Sets purple glow effect
        /// </summary>
        public static void SetPurpleGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(0.8f, 0.2f, 1.0f), intensity);
        }

        /// <summary>
        /// Sets white glow effect
        /// </summary>
        public static void SetWhiteGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(1.0f, 1.0f, 1.0f), intensity);
        }

        /// <summary>
        /// Sets orange glow effect
        /// </summary>
        public static void SetOrangeGlow(this ModelObject obj, float intensity = 1.0f)
        {
            obj.SetGlow(new Vector3(1.0f, 0.5f, 0.0f), intensity);
        }
    }
}