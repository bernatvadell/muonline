using System.Collections.Generic;
using Client.Main.Core.Utilities;
using Client.Main.Models;
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
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                lines.Add(($"{dmgType} Damage : {def.DamageMin} ~ {def.DamageMax}", Color.Orange));
            }

            if (def.Defense > 0) lines.Add(($"Defense     : {def.Defense}", Color.Orange));
            if (def.DefenseRate > 0) lines.Add(($"Defense Rate: {def.DefenseRate}", Color.Orange));
            if (def.AttackSpeed > 0) lines.Add(($"Attack Speed: {def.AttackSpeed}", Color.Orange));
            lines.Add(($"Durability : {item.Durability}/{def.BaseDurability}", Color.Silver));
            if (def.RequiredLevel > 0) lines.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));
            if (def.RequiredStrength > 0) lines.Add(($"Required Strength: {def.RequiredStrength}", Color.LightGray));
            if (def.RequiredDexterity > 0) lines.Add(($"Required Agility : {def.RequiredDexterity}", Color.LightGray));
            if (def.RequiredEnergy > 0) lines.Add(($"Required Energy  : {def.RequiredEnergy}", Color.LightGray));

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

            if (def.IsConsumable())
                lines.Add(("Right-click to use", new Color(255, 215, 0)));

            return lines;
        }
    }
}
