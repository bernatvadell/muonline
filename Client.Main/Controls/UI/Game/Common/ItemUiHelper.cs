using System;
using System.Collections.Generic;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Controls.UI.Game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Content;

namespace Client.Main.Controls.UI.Game.Common
{
    public readonly struct ItemGlowPalette
    {
        public ItemGlowPalette(Color normal, Color magic, Color excellent, Color ancient, Color legendary)
        {
            Normal = normal;
            Magic = magic;
            Excellent = excellent;
            Ancient = ancient;
            Legendary = legendary;
        }

        public Color Normal { get; }
        public Color Magic { get; }
        public Color Excellent { get; }
        public Color Ancient { get; }
        public Color Legendary { get; }
    }

    public static class ItemUiHelper
    {
        /// <summary>
        /// Calculates max durability based on item level, excellent, and ancient options.
        /// Formula from ZzzInventory.cpp:CalcMaxDurability()
        /// </summary>
        public static int CalculateMaxDurability(ItemDefinition def, ItemDatabase.ItemDetails details, bool isStaff)
        {
            // Start with base durability (staffs use MagicDurability, others use BaseDurability)
            int maxDur = isStaff ? def.MagicDurability : def.BaseDurability;

            // Add level bonuses
            int level = details.Level;
            for (int i = 0; i < level; i++)
            {
                if (i >= 14)      // Level 15
                    maxDur = Math.Min(maxDur + 8, 255);
                else if (i >= 13) // Level 14
                    maxDur = Math.Min(maxDur + 7, 255);
                else if (i >= 12) // Level 13
                    maxDur += 6;
                else if (i >= 11) // Level 12
                    maxDur += 5;
                else if (i >= 10) // Level 11
                    maxDur += 4;
                else if (i >= 9)  // Level 10
                    maxDur += 3;
                else if (i >= 4)  // Level 5-9
                    maxDur += 2;
                else              // Level 1-4
                    maxDur++;
            }

            // Ancient items: +20 durability
            if (details.IsAncient)
            {
                maxDur += 20;
            }
            // Excellent items (non-wings): +15 durability
            else if (details.IsExcellent && def.Group != 12) // Group 12 = Wings
            {
                maxDur += 15;
            }

            return Math.Min(maxDur, 255);
        }

        /// <summary>
        /// Calculates damage with level bonus.
        /// Formula from ZzzInfomation.cpp (line 501-514)
        /// </summary>
        public static (int min, int max) CalculateDamageWithLevel(int baseMin, int baseMax, int level, bool isExcellent, int dropLevel)
        {
            int dmgMin = baseMin;
            int dmgMax = baseMax;

            if (baseMin > 0)
            {
                if (isExcellent && dropLevel > 0)
                    dmgMin += baseMin * 25 / dropLevel + 5;
                dmgMin += level * 3;
            }

            if (baseMax > 0)
            {
                if (isExcellent && dropLevel > 0)
                    dmgMax += baseMin * 25 / dropLevel + 5;
                dmgMax += level * 3;
            }

            return (dmgMin, dmgMax);
        }

        /// <summary>
        /// Calculates defense with level bonus.
        /// Formula from ZzzInfomation.cpp (line 516-530)
        /// </summary>
        public static int CalculateDefenseWithLevel(int baseDefense, int level, bool isShield, bool isExcellent, int dropLevel)
        {
            int defense = baseDefense;

            if (defense > 0)
            {
                if (isShield)
                {
                    defense += level; // Shields: +level
                }
                else
                {
                    if (isExcellent && dropLevel > 0)
                        defense += baseDefense * 12 / dropLevel + 4 + dropLevel / 5;
                    defense += level * 3; // Non-shields: +level*3
                }
            }

            return defense;
        }

        /// <summary>
        /// Calculates requirements with level and excellent bonuses.
        /// Formula from ZzzInfomation.cpp (line 540-551)
        /// </summary>
        public static int CalculateRequirement(int baseRequirement, int level, bool isExcellent, int dropLevel)
        {
            if (baseRequirement == 0) return 0;

            int itemLevel = dropLevel;
            if (isExcellent)
                itemLevel += 25; // Excellent adds +25 to ItemLevel for requirements

            return 20 + baseRequirement * (itemLevel + level * 3) * 3 / 100;
        }

        public static Color GetItemGlowColor(InventoryItem item, ItemGlowPalette palette)
        {
            if (item?.Details is null)
                return palette.Normal;

            if (item.Details.IsExcellent) return palette.Excellent;
            if (item.Details.IsAncient) return palette.Ancient;
            if (item.Details.Level >= 9) return palette.Legendary;
            if (item.Details.Level >= 5) return palette.Magic;
            return palette.Normal;
        }

        public static void DrawItemGlow(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int glowSize = 4)
        {
            if (spriteBatch == null || pixel == null) return;

            for (int i = glowSize; i > 0; i--)
            {
                float alpha = (float)(glowSize - i + 1) / glowSize * 0.6f;
                Color layerColor = color * alpha;

                var glowRect = new Rectangle(rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2);

                spriteBatch.Draw(pixel, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width, 1), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(glowRect.X, glowRect.Bottom - 1, glowRect.Width, 1), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(glowRect.X, glowRect.Y, 1, glowRect.Height), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(glowRect.Right - 1, glowRect.Y, 1, glowRect.Height), layerColor);
            }
        }

        public static List<(string text, Color color)> BuildTooltipLines(InventoryItem item)
        {
            var lines = new List<(string, Color)>();
            if (item?.Definition == null || item?.Details is null)
                return lines;

            var details = item.Details;

            string name = details.IsExcellent ? $"Excellent {item.Definition.Name}"
                        : details.IsAncient ? $"Ancient {item.Definition.Name}"
                        : item.Definition.Name;

            if (details.Level > 0)
                name += $" +{details.Level}";

            lines.Add((name, Color.White));

            var def = item.Definition;
            int itemLevel = details.Level;
            bool isExcellent = details.IsExcellent;
            bool isStaff = def.Group == 5;
            bool isShield = def.Group == 6;

            // Physical damage (all weapons including staffs)
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                var (dmgMin, dmgMax) = CalculateDamageWithLevel(def.DamageMin, def.DamageMax, itemLevel, isExcellent, def.DropLevel);

                // Staffs have special calculation: DamageMin / 2 + Level * 2
                if (isStaff)
                {
                    dmgMin = dmgMin / 2 + itemLevel * 2;
                    dmgMax = 0;
                }

                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                if (dmgMax > 0)
                    lines.Add(($"{dmgType} Damage : {dmgMin} ~ {dmgMax}", Color.Orange));
                else
                    lines.Add(($"{dmgType} Damage : {dmgMin}", Color.Orange));
            }

            // Wizard damage (for staffs and other magic weapons)
            if (def.MagicPower > 0)
            {
                lines.Add(($"Wizardry Damage : {def.MagicPower}%", new Color(150, 200, 255)));
            }

            // Defense
            if (def.Defense > 0)
            {
                int defense = CalculateDefenseWithLevel(def.Defense, itemLevel, isShield, isExcellent, def.DropLevel);
                lines.Add(($"Defense     : {defense}", Color.Orange));
            }

            if (def.DefenseRate > 0) lines.Add(($"Defense Rate: {def.DefenseRate}", Color.Orange));
            if (def.AttackSpeed > 0) lines.Add(($"Attack Speed: {def.AttackSpeed}", Color.Orange));

            // Movement speed for boots/gloves
            if (def.WalkSpeed > 0)
            {
                lines.Add(($"Movement Speed : {def.WalkSpeed}", new Color(100, 255, 100)));
            }

            // Max durability with level/excellent/ancient bonuses
            int maxDurability = CalculateMaxDurability(def, details, isStaff);

            // Only show durability if item has durability (maxDurability > 0)
            // This excludes jewels, scrolls, and other items without durability
            if (maxDurability > 0)
            {
                lines.Add(($"Durability : {item.Durability}/{maxDurability}", Color.Silver));
            }

            if (def.RequiredLevel > 0) lines.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));

            // Requirements with level/excellent bonuses
            if (def.RequiredStrength > 0)
            {
                int reqStr = CalculateRequirement(def.RequiredStrength, itemLevel, isExcellent, def.DropLevel);
                lines.Add(($"Required Strength: {reqStr}", Color.LightGray));
            }
            if (def.RequiredDexterity > 0)
            {
                int reqDex = CalculateRequirement(def.RequiredDexterity, itemLevel, isExcellent, def.DropLevel);
                lines.Add(($"Required Agility : {reqDex}", Color.LightGray));
            }
            if (def.RequiredEnergy > 0)
            {
                int reqEne = 20 + def.RequiredEnergy * (def.DropLevel + itemLevel * 3) * 4 / 10; // Energy uses different formula
                lines.Add(($"Required Energy  : {reqEne}", Color.LightGray));
            }

            if (def.AllowedClasses != null)
            {
                foreach (var cls in def.AllowedClasses)
                    lines.Add(($"Can be equipped by {cls}", Color.LightGray));
            }

            if (details.OptionLevel > 0)
                lines.Add(($"Additional Option : +{details.OptionLevel * 4}", new Color(80, 255, 80)));

            if (details.HasLuck) lines.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (details.HasSkill) lines.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));

            if (details.IsExcellent)
            {
                byte excByte = item.RawData.Length > 3 ? item.RawData[3] : (byte)0;
                foreach (var option in ItemDatabase.ParseExcellentOptions(excByte))
                    lines.Add(($"+{option}", new Color(128, 255, 128)));
            }

            if (details.IsAncient)
                lines.Add(("Ancient Option", new Color(0, 255, 128)));

            // Show "Right-click to use" for consumables (potions, scrolls), but not for jewels
            if (def.IsConsumable() && !def.IsJewel())
                lines.Add(("Right-click to use", new Color(255, 215, 0)));

            // Add repair cost if in repair mode
            var npcShop = NpcShopControl.Instance;
            if (npcShop != null && npcShop.Visible && npcShop.IsRepairMode)
            {
                if (Core.Utilities.ItemPriceCalculator.IsRepairable(item))
                {
                    int repairCost = Core.Utilities.ItemPriceCalculator.CalculateRepairPrice(item, npcDiscount: true);
                    if (repairCost > 0 && item.Durability < maxDurability)
                    {
                        lines.Add(($"Repair Cost: {repairCost} Zen", new Color(212, 175, 85)));
                    }
                }
                else
                {
                    lines.Add(("Cannot be repaired", new Color(255, 100, 100)));
                }
            }

            return lines;
        }
    }
}
