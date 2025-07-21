using System.Collections.Generic;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class ItemDefinition
    {
        public int Id { get; set; } // Unique ID of the item type (e.g., from Item.bmd)
        public string Name { get; set; }
        public int Width { get; set; }  // Width in slots
        public int Height { get; set; } // Height in slots
        public string TexturePath { get; set; } // Path to the item's texture/model

        // Additional stats loaded from items.json for richer tooltips
        public int DamageMin { get; set; }
        public int DamageMax { get; set; }
        public int AttackSpeed { get; set; }
        public int Defense { get; set; }
        public int DefenseRate { get; set; }
        public int BaseDurability { get; set; }
        public int RequiredStrength { get; set; }
        public int RequiredDexterity { get; set; }
        public int RequiredEnergy { get; set; }
        public int RequiredLevel { get; set; }
        public bool TwoHanded { get; set; }
        public int Group { get; set; }

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
    }
}