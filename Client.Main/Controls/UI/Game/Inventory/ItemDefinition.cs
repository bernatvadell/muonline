using System.Collections.Generic;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class ItemDefinition
    {
        private static readonly HashSet<int> s_upgradeJewelIds = new() { 13, 14, 16 };
        public int Id { get; set; } // Unique ID of the item type (e.g., from Item.bmd)
        public string Name { get; set; }
        public int Width { get; set; }  // Width in slots
        public int Height { get; set; } // Height in slots
        public string TexturePath { get; set; } // Path to the item's texture/model

        // Additional stats loaded from items.json for richer tooltips
        public int DamageMin { get; set; }
        public int DamageMax { get; set; }
        public int MagicPower { get; set; }  // Wizard damage for staffs
        public int AttackSpeed { get; set; }
        public int Defense { get; set; }
        public int DefenseRate { get; set; }
        public int BaseDurability { get; set; }
        public int MagicDurability { get; set; }  // Max durability for staffs (uses MagicDur instead of Durability)
        public int WalkSpeed { get; set; }
        public int RequiredStrength { get; set; }
        public int RequiredDexterity { get; set; }
        public int RequiredEnergy { get; set; }
        public int RequiredLevel { get; set; }
        public bool TwoHanded { get; set; }
        public int Group { get; set; }
        public bool IsExpensive { get; set; }
        public bool CanSellToNpc { get; set; }
        public int Money { get; set; } // Base buy price (iZen) from Item.bmd, can be 0
        public int ItemValue { get; set; } // Legacy value fallback if Money is missing
        public int DropLevel { get; set; } // Drop level from BMD, used as a proxy for price curve

        // Classes which can equip this item
        public List<string> AllowedClasses { get; set; } = new();

        public ItemDefinition(int id, string name, int width, int height, string texturePath = null)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            TexturePath = texturePath;
        }

        /// <summary>
        /// Checks if this item is consumable (potions, scrolls, etc.).
        /// </summary>
        public bool IsConsumable()
        {
            // Group 14 = Potions (HP, MP, SD potions)
            // Group 15 = Scrolls
            return Group == 14 || Group == 15;
        }

        /// <summary>
        /// Determines if the item is a jewel (non-consumable items in group 14/12).
        /// Jewels should not show "Right-click to use" even though they're in consumable groups.
        /// </summary>
        public bool IsJewel()
        {
            // Group 14 jewels: Bless (13), Soul (14), Life (16), Creation (22), Guardian (31), etc.
            // Group 12 jewels: Chaos (15), etc.
            if (Group == 14 && (Id == 13 || Id == 14 || Id == 16 || Id == 22 || Id == 31))
                return true;
            if (Group == 12 && Id == 15)
                return true;
            return false;
        }

        /// <summary>
        /// Determines if the item is an upgrade jewel (Bless, Soul, Life).
        /// </summary>
        public bool IsUpgradeJewel()
        {
            return Group == 14 && s_upgradeJewelIds.Contains(Id);
        }
    }
}
