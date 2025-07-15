using Microsoft.Xna.Framework;
using Client.Main.Objects;
using Client.Main.Extensions;

namespace Client.Main.Examples
{
    /// <summary>
    /// Example demonstrating how to use the monster/NPC material shader system
    /// </summary>
    public static class ShaderUsageExample
    {
        /// <summary>
        /// Example of applying different glow effects to monsters/NPCs
        /// </summary>
        public static void ApplyGlowEffects()
        {
            // Assume we have monster/NPC objects
            ModelObject monster = null; // Your monster object
            ModelObject npc = null;     // Your NPC object
            
            // Method 1: Using extension methods (recommended for common colors)
            monster?.SetGoldGlow(1.5f);     // Gold glow with 1.5x intensity
            monster?.SetRedGlow(2.0f);      // Red glow with 2.0x intensity
            monster?.SetBlueGlow(1.0f);     // Blue glow with normal intensity
            
            // Method 2: Using custom colors
            monster?.SetGlow(new Vector3(0.8f, 0.2f, 1.0f), 1.8f); // Purple glow
            
            // Method 3: Direct property setting
            if (npc != null)
            {
                npc.GlowColor = new Vector3(1.0f, 0.5f, 0.0f); // Orange
                npc.GlowIntensity = 2.5f;                       // High intensity
                npc.EnableCustomShader = true;                 // Enable the shader
            }
            
            // Disable glow
            monster?.DisableGlow();
        }
        
        /// <summary>
        /// Example of setting up boss monsters with special effects
        /// </summary>
        public static void SetupBossMonster(ModelObject bossMonster)
        {
            if (bossMonster == null) return;
            
            // Ultra-bright gold glow for boss
            bossMonster.SetGoldGlow(3.0f); // Very high intensity for dramatic effect
        }
        
        /// <summary>
        /// Example of setting up rare/elite monsters
        /// </summary>
        public static void SetupEliteMonster(ModelObject eliteMonster)
        {
            if (eliteMonster == null) return;
            
            // Blue glow for elite monsters
            eliteMonster.SetBlueGlow(1.5f);
        }
        
        /// <summary>
        /// Example of setting up friendly NPCs
        /// </summary>
        public static void SetupFriendlyNPC(ModelObject npc)
        {
            if (npc == null) return;
            
            // Gentle green glow for friendly NPCs
            npc.SetGreenGlow(0.8f);
        }
        
        /// <summary>
        /// Example of setting up quest NPCs
        /// </summary>
        public static void SetupQuestNPC(ModelObject questNpc)
        {
            if (questNpc == null) return;
            
            // White glow for quest NPCs
            questNpc.SetWhiteGlow(1.2f);
        }
        
        /// <summary>
        /// Example of conditional glow based on monster status
        /// </summary>
        public static void UpdateMonsterGlow(ModelObject monster, bool isAngry, bool isLowHealth)
        {
            if (monster == null) return;
            
            if (isAngry && isLowHealth)
            {
                // Intense red glow when angry and low health
                monster.SetRedGlow(2.5f);
            }
            else if (isAngry)
            {
                // Orange glow when just angry
                monster.SetOrangeGlow(1.5f);
            }
            else if (isLowHealth)
            {
                // Purple glow when low health
                monster.SetPurpleGlow(1.0f);
            }
            else
            {
                // No special glow in normal state
                monster.DisableGlow();
            }
        }
    }
}