using System;
using System.Collections.Generic;
using System.Linq; // Required for OrderBy, ThenBy, ToList

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// A static class containing the item name database based on their group and ID.
    /// Data is generated from OpenMU initializers (Season 6).
    /// </summary>
    public static class ItemDatabase
    {
        // NOTE: The actual item data initialization is omitted here for brevity,
        // as requested. Assume the 'Items' dictionary is populated correctly
        // based on the original provided code.

        /// <summary>
        /// Main dictionary: Outer key is Group (byte), Value is an inner dictionary.
        /// Inner dictionary: Key is Number/ID (short), Value is Name (string).
        /// </summary>
        public static readonly Dictionary<byte, Dictionary<short, string>> Items = InitializeItemData();

        private static Dictionary<byte, Dictionary<short, string>> InitializeItemData()
        {
            var data = new Dictionary<byte, Dictionary<short, string>>();

            // Helper function for adding items to the dictionary
            void AddItem(byte group, short id, string name)
            {
                if (!data.TryGetValue(group, out var groupDict))
                {
                    groupDict = new Dictionary<short, string>();
                    data[group] = groupDict;
                }

                // Handle compound names (e.g., "Potion of Bless;Potion of Soul") - take only the first part
                string finalName = name.Contains(';') ? name.Split(';')[0].Trim() : name;

                if (!groupDict.ContainsKey(id))
                {
                    groupDict.Add(id, finalName);
                }
                else
                {
                    // Optionally: Log a warning about duplicate keys, if unexpected
                    // Console.WriteLine($"Warning: Duplicate key for item Group={group}, ID={id}, Name='{finalName}' (existing: '{groupDict[id]}'). Keeping existing.");
                }
            }

            // --- Armors.cs ---
            // Shields (Group 6)
            AddItem(6, 0, "Small Shield");
            AddItem(6, 1, "Horn Shield");
            AddItem(6, 2, "Kite Shield");
            AddItem(6, 3, "Elven Shield");
            AddItem(6, 4, "Buckler");
            AddItem(6, 5, "Dragon Slayer Shield");
            AddItem(6, 6, "Skull Shield");
            AddItem(6, 7, "Spiked Shield");
            AddItem(6, 8, "Tower Shield");
            AddItem(6, 9, "Plate Shield");
            AddItem(6, 10, "Big Round Shield");
            AddItem(6, 11, "Serpent Shield");
            AddItem(6, 12, "Bronze Shield");
            AddItem(6, 13, "Dragon Shield");
            AddItem(6, 14, "Legendary Shield");
            AddItem(6, 15, "Grand Soul Shield");
            AddItem(6, 16, "Elemental Shield");
            AddItem(6, 17, "Crimson Glory");
            AddItem(6, 18, "Salamander Shield");
            AddItem(6, 19, "Frost Barrier");
            AddItem(6, 20, "Guardian Shield");
            AddItem(6, 21, "Cross Shield");

            // Helms (Group 7) - Extracted from CreateArmor(id, 2, ...)
            AddItem(7, 0, "Bronze Helm");
            AddItem(7, 1, "Dragon Helm");
            AddItem(7, 2, "Pad Helm");
            AddItem(7, 3, "Legendary Helm");
            AddItem(7, 4, "Bone Helm");
            AddItem(7, 5, "Leather Helm");
            AddItem(7, 6, "Scale Helm");
            AddItem(7, 7, "Sphinx Mask");
            AddItem(7, 8, "Brass Helm");
            AddItem(7, 9, "Plate Helm");
            AddItem(7, 10, "Vine Helm");
            AddItem(7, 11, "Silk Helm");
            AddItem(7, 12, "Wind Helm");
            AddItem(7, 13, "Spirit Helm");
            AddItem(7, 14, "Guardian Helm");
            AddItem(7, 16, "Black Dragon Helm");
            AddItem(7, 17, "Dark Phoenix Helm");
            AddItem(7, 18, "Grand Soul Helm");
            AddItem(7, 19, "Divine Helm");
            AddItem(7, 21, "Great Dragon Helm");
            AddItem(7, 22, "Dark Soul Helm");
            AddItem(7, 24, "Red Spirit Helm");
            AddItem(7, 25, "Light Plate Mask");
            AddItem(7, 26, "Adamantine Mask");
            AddItem(7, 27, "Dark Steel Mask");
            AddItem(7, 28, "Dark Master Mask");
            AddItem(7, 29, "Dragon Knight Helm");
            AddItem(7, 30, "Venom Mist Helm");
            AddItem(7, 31, "Sylphid Ray Helm");
            AddItem(7, 33, "Sunlight Mask");
            AddItem(7, 34, "Ashcrow Helm");
            AddItem(7, 35, "Eclipse Helm");
            AddItem(7, 36, "Iris Helm");
            AddItem(7, 38, "Glorious Mask");
            AddItem(7, 39, "Mistery Helm");
            AddItem(7, 40, "Red Wing Helm");
            AddItem(7, 41, "Ancient Helm");
            AddItem(7, 42, "Black Rose Helm");
            AddItem(7, 43, "Aura Helm");
            AddItem(7, 44, "Lilium Helm");
            AddItem(7, 45, "Titan Helm");
            AddItem(7, 46, "Brave Helm");
            AddItem(7, 49, "Seraphim Helm");
            AddItem(7, 50, "Faith Helm");
            AddItem(7, 51, "Paewang Mask");
            AddItem(7, 52, "Hades Helm");
            AddItem(7, 59, "Sacred Helm");
            AddItem(7, 60, "Storm Hard Helm");
            AddItem(7, 61, "Piercing Helm");
            AddItem(7, 73, "Phoenix Soul Helmet");

            // Armors (Group 8) - Extracted from CreateArmor(id, 3, ...)
            AddItem(8, 0, "Bronze Armor");
            AddItem(8, 1, "Dragon Armor");
            AddItem(8, 2, "Pad Armor");
            AddItem(8, 3, "Legendary Armor");
            AddItem(8, 4, "Bone Armor");
            AddItem(8, 5, "Leather Armor");
            AddItem(8, 6, "Scale Armor");
            AddItem(8, 7, "Sphinx Armor");
            AddItem(8, 8, "Brass Armor");
            AddItem(8, 9, "Plate Armor");
            AddItem(8, 10, "Vine Armor");
            AddItem(8, 11, "Silk Armor");
            AddItem(8, 12, "Wind Armor");
            AddItem(8, 13, "Spirit Armor");
            AddItem(8, 14, "Guardian Armor");
            AddItem(8, 15, "Storm Crow Armor");
            AddItem(8, 16, "Black Dragon Armor");
            AddItem(8, 17, "Dark Phoenix Armor");
            AddItem(8, 18, "Grand Soul Armor");
            AddItem(8, 19, "Divine Armor");
            AddItem(8, 20, "Thunder Hawk Armor");
            AddItem(8, 21, "Great Dragon Armor");
            AddItem(8, 22, "Dark Soul Armor");
            AddItem(8, 23, "Hurricane Armor");
            AddItem(8, 24, "Red Sprit Armor");
            AddItem(8, 25, "Light Plate Armor");
            AddItem(8, 26, "Adamantine Armor");
            AddItem(8, 27, "Dark Steel Armor");
            AddItem(8, 28, "Dark Master Armor");
            AddItem(8, 29, "Dragon Knight Armor");
            AddItem(8, 30, "Venom Mist Armor");
            AddItem(8, 31, "Sylphid Ray Armor");
            AddItem(8, 32, "Volcano Armor");
            AddItem(8, 33, "Sunlight Armor");
            AddItem(8, 34, "Ashcrow Armor");
            AddItem(8, 35, "Eclipse Armor");
            AddItem(8, 36, "Iris Armor");
            AddItem(8, 37, "Valiant Armor");
            AddItem(8, 38, "Glorious Armor");
            AddItem(8, 39, "Mistery Armor");
            AddItem(8, 40, "Red Wing Armor");
            AddItem(8, 41, "Ancient Armor");
            AddItem(8, 42, "Black Rose Armor");
            AddItem(8, 43, "Aura Armor");
            AddItem(8, 44, "Lilium Armor");
            AddItem(8, 45, "Titan Armor");
            AddItem(8, 46, "Brave Armor");
            AddItem(8, 47, "Destory Armor");
            AddItem(8, 48, "Phantom Armor");
            AddItem(8, 49, "Seraphim Armor");
            AddItem(8, 50, "Faith Armor");
            AddItem(8, 51, "Paewang Armor");
            AddItem(8, 52, "Hades Armor");
            AddItem(8, 59, "Sacred Armor");
            AddItem(8, 60, "Storm Hard Armor");
            AddItem(8, 61, "Piercing Armor");
            AddItem(8, 73, "Phoenix Soul Armor");

            // Pants (Group 9) - Extracted from CreateArmor(id, 4, ...)
            AddItem(9, 0, "Bronze Pants");
            AddItem(9, 1, "Dragon Pants");
            AddItem(9, 2, "Pad Pants");
            AddItem(9, 3, "Legendary Pants");
            AddItem(9, 4, "Bone Pants");
            AddItem(9, 5, "Leather Pants");
            AddItem(9, 6, "Scale Pants");
            AddItem(9, 7, "Sphinx Pants");
            AddItem(9, 8, "Brass Pants");
            AddItem(9, 9, "Plate Pants");
            AddItem(9, 10, "Vine Pants");
            AddItem(9, 11, "Silk Pants");
            AddItem(9, 12, "Wind Pants");
            AddItem(9, 13, "Spirit Pants");
            AddItem(9, 14, "Guardian Pants");
            AddItem(9, 15, "Storm Crow Pants");
            AddItem(9, 16, "Black Dragon Pants");
            AddItem(9, 17, "Dark Phoenix Pants");
            AddItem(9, 18, "Grand Soul Pants");
            AddItem(9, 19, "Divine Pants");
            AddItem(9, 20, "Thunder Hawk Pants");
            AddItem(9, 21, "Great Dragon Pants");
            AddItem(9, 22, "Dark Soul Pants");
            AddItem(9, 23, "Hurricane Pants");
            AddItem(9, 24, "Red Spirit Pants");
            AddItem(9, 25, "Light Plate Pants");
            AddItem(9, 26, "Adamantine Pants");
            AddItem(9, 27, "Dark Steel Pants");
            AddItem(9, 28, "Dark Master Pants");
            AddItem(9, 29, "Dragon Knight Pants");
            AddItem(9, 30, "Venom Mist Pants");
            AddItem(9, 31, "Sylphid Ray Pants");
            AddItem(9, 32, "Volcano Pants");
            AddItem(9, 33, "Sunlight Pants");
            AddItem(9, 34, "Ashcrow Pants");
            AddItem(9, 35, "Eclipse Pants");
            AddItem(9, 36, "Iris Pants");
            AddItem(9, 37, "Valiant Pants");
            AddItem(9, 38, "Glorious Pants");
            AddItem(9, 39, "Mistery Pants");
            AddItem(9, 40, "Red Wing Pants");
            AddItem(9, 41, "Ancient Pants");
            AddItem(9, 42, "Black Rose Pants");
            AddItem(9, 43, "Aura Pants");
            AddItem(9, 44, "Lilium Pants");
            AddItem(9, 45, "Titan Pants");
            AddItem(9, 46, "Brave Pants");
            AddItem(9, 47, "Destory Pants");
            AddItem(9, 48, "Phantom Pants");
            AddItem(9, 49, "Seraphim Pants");
            AddItem(9, 50, "Faith Pants");
            AddItem(9, 51, "Paewang Pants");
            AddItem(9, 52, "Hades Pants");
            AddItem(9, 59, "Sacred Pants");
            AddItem(9, 60, "Storm Hard Pants");
            AddItem(9, 61, "Piercing Pants");
            AddItem(9, 73, "Phoenix Soul Pants");

            // Gloves (Group 10) - Extracted from CreateGloves(id, ...)
            AddItem(10, 0, "Bronze Gloves");
            AddItem(10, 1, "Dragon Gloves");
            AddItem(10, 2, "Pad Gloves");
            AddItem(10, 3, "Legendary Gloves");
            AddItem(10, 4, "Bone Gloves");
            AddItem(10, 5, "Leather Gloves");
            AddItem(10, 6, "Scale Gloves");
            AddItem(10, 7, "Sphinx Gloves");
            AddItem(10, 8, "Brass Gloves");
            AddItem(10, 9, "Plate Gloves");
            AddItem(10, 10, "Vine Gloves");
            AddItem(10, 11, "Silk Gloves");
            AddItem(10, 12, "Wind Gloves");
            AddItem(10, 13, "Spirit Gloves");
            AddItem(10, 14, "Guardian Gloves");
            AddItem(10, 15, "Storm Crow Gloves");
            AddItem(10, 16, "Black Dragon Gloves");
            AddItem(10, 17, "Dark Phoenix Gloves");
            AddItem(10, 18, "Grand Soul Gloves");
            AddItem(10, 19, "Divine Gloves");
            AddItem(10, 20, "Thunder Hawk Gloves");
            AddItem(10, 21, "Great Dragon Gloves");
            AddItem(10, 22, "Dark Soul Gloves");
            AddItem(10, 23, "Hurricane Gloves");
            AddItem(10, 24, "Red Spirit Gloves");
            AddItem(10, 25, "Light Plate Gloves");
            AddItem(10, 26, "Adamantine Gloves");
            AddItem(10, 27, "Dark Steel Gloves");
            AddItem(10, 28, "Dark Master Gloves");
            AddItem(10, 29, "Dragon Knight Gloves");
            AddItem(10, 30, "Venom Mist Gloves");
            AddItem(10, 31, "Sylphid Ray Gloves");
            AddItem(10, 32, "Volcano Gloves");
            AddItem(10, 33, "Sunlight Gloves");
            AddItem(10, 34, "Ashcrow Gloves");
            AddItem(10, 35, "Eclipse Gloves");
            AddItem(10, 36, "Iris Gloves");
            AddItem(10, 37, "Valiant Gloves");
            AddItem(10, 38, "Glorious Gloves");
            AddItem(10, 39, "Mistery Gloves");
            AddItem(10, 40, "Red Wing Gloves");
            AddItem(10, 41, "Ancient Gloves");
            AddItem(10, 42, "Black Rose Gloves");
            AddItem(10, 43, "Aura Gloves");
            AddItem(10, 44, "Lilium Gloves");
            AddItem(10, 45, "Titan Gloves");
            AddItem(10, 46, "Brave Gloves");
            AddItem(10, 47, "Destroy Gloves");
            AddItem(10, 48, "Phantom Gloves");
            AddItem(10, 49, "Seraphim Gloves");
            AddItem(10, 50, "Faith Gloves");
            AddItem(10, 51, "Paewang Gloves");
            AddItem(10, 52, "Hades Gloves");

            // Boots (Group 11) - Extracted from CreateBoots(id, ...)
            AddItem(11, 0, "Bronze Boots");
            AddItem(11, 1, "Dragon Boots");
            AddItem(11, 2, "Pad Boots");
            AddItem(11, 3, "Legendary Boots");
            AddItem(11, 4, "Bone Boots");
            AddItem(11, 5, "Leather Boots");
            AddItem(11, 6, "Scale Boots");
            AddItem(11, 7, "Sphinx Boots");
            AddItem(11, 8, "Brass Boots");
            AddItem(11, 9, "Plate Boots");
            AddItem(11, 10, "Vine Boots");
            AddItem(11, 11, "Silk Boots");
            AddItem(11, 12, "Wind Boots");
            AddItem(11, 13, "Spirit Boots");
            AddItem(11, 14, "Guardian Boots");
            AddItem(11, 15, "Storm Crow Boots");
            AddItem(11, 16, "Black Dragon Boots");
            AddItem(11, 17, "Dark Phoenix Boots");
            AddItem(11, 18, "Grand Soul Boots");
            AddItem(11, 19, "Divine Boots");
            AddItem(11, 20, "Thunder Hawk Boots");
            AddItem(11, 21, "Great Dragon Boots");
            AddItem(11, 22, "Dark Soul Boots");
            AddItem(11, 23, "Hurricane Boots");
            AddItem(11, 24, "Red Spirit Boots");
            AddItem(11, 25, "Light Plate Boots");
            AddItem(11, 26, "Adamantine Boots");
            AddItem(11, 27, "Dark Steel Boots");
            AddItem(11, 28, "Dark Master Boots");
            AddItem(11, 29, "Dragon Knight Boots");
            AddItem(11, 30, "Venom Mist Boots");
            AddItem(11, 31, "Sylphid Ray Boots");
            AddItem(11, 32, "Volcano Boots");
            AddItem(11, 33, "Sunlight Boots");
            AddItem(11, 34, "Ashcrow Boots");
            AddItem(11, 35, "Eclipse Boots");
            AddItem(11, 36, "Iris Boots");
            AddItem(11, 37, "Valiant Boots");
            AddItem(11, 38, "Glorious Boots");
            AddItem(11, 39, "Mistery Boots");
            AddItem(11, 40, "Red Wing Boots");
            AddItem(11, 41, "Ancient Boots");
            AddItem(11, 42, "Black Rose Boots");
            AddItem(11, 43, "Aura Boots");
            AddItem(11, 44, "Lilium Boots");
            AddItem(11, 45, "Titan Boots");
            AddItem(11, 46, "Brave Boots");
            AddItem(11, 47, "Destory Boots");
            AddItem(11, 48, "Phantom Boots");
            AddItem(11, 49, "Seraphim Boots");
            AddItem(11, 50, "Faith Boots");
            AddItem(11, 51, "Phaewang Boots"); // Paewang Boots
            AddItem(11, 52, "Hades Boots");
            AddItem(11, 59, "Sacred Boots");
            AddItem(11, 60, "Storm Hard Boots");
            AddItem(11, 61, "Piercing Boots");
            AddItem(11, 73, "Phoenix Soul Boots");

            // --- BoxOfLuck.cs ---
            AddItem(14, 11, "Box of Luck");
            AddItem(14, 32, "Pink Chocolate Box");
            AddItem(14, 33, "Red Chocolate Box");
            AddItem(14, 34, "Blue Chocolate Box");
            AddItem(14, 45, "Pumpkin of Luck");
            AddItem(12, 32, "Red Ribbon Box");
            AddItem(12, 33, "Green Ribbon Box");
            AddItem(12, 34, "Blue Ribbon Box");
            AddItem(14, 51, "Christmas Star");
            AddItem(14, 63, "Firecracker");
            AddItem(14, 84, "Cherry Blossom Play-Box");
            AddItem(14, 99, "Christmas Firecracker");
            AddItem(14, 52, "GM Gift");
            AddItem(13, 20, "Wizard's Ring");

            // --- EventTicketItems.cs ---
            AddItem(13, 16, "Scroll of Archangel");
            AddItem(13, 17, "Blood Bone");
            AddItem(13, 18, "Invisibility Cloak");
            AddItem(13, 19, "Weapon of Archangel");
            AddItem(13, 29, "Armor of Guardsman");
            AddItem(13, 49, "Old Scroll");
            AddItem(13, 50, "Illusion Sorcerer Covenant");
            AddItem(13, 51, "Scroll of Blood");
            AddItem(14, 17, "Devil's Eye");
            AddItem(14, 18, "Devil's Key");
            AddItem(14, 19, "Devil's Invitation");
            AddItem(14, 101, "Suspicious Scrap of Paper");
            AddItem(14, 102, "Gaion's Order");
            AddItem(14, 103, "First Secromicon Fragment");
            AddItem(14, 104, "Second Secromicon Fragment");
            AddItem(14, 105, "Third Secromicon Fragment");
            AddItem(14, 106, "Fourth Secromicon Fragment");
            AddItem(14, 107, "Fifth Secromicon Fragment");
            AddItem(14, 108, "Sixth Secromicon Fragment");
            AddItem(14, 109, "Complete Secromicon");

            // --- Jewels.cs ---
            AddItem(14, 13, "Jewel of Bless");
            AddItem(14, 14, "Jewel of Soul");
            AddItem(12, 15, "Jewel of Chaos");
            AddItem(14, 16, "Jewel of Life");
            AddItem(14, 22, "Jewel of Creation");
            AddItem(14, 31, "Jewel of Guardian");
            AddItem(14, 41, "Gemstone");
            AddItem(14, 42, "Jewel of Harmony");
            AddItem(14, 43, "Lower refine stone");
            AddItem(14, 44, "Higher refine stone");

            // --- Jewelery.cs ---
            AddItem(13, 8, "Ring of Ice");
            AddItem(13, 9, "Ring of Poison");
            AddItem(13, 12, "Pendant of Lighting");
            AddItem(13, 13, "Pendant of Fire");
            AddItem(13, 21, "Ring of Fire");
            AddItem(13, 22, "Ring of Earth");
            AddItem(13, 23, "Ring of Wind");
            AddItem(13, 24, "Ring of Magic");
            AddItem(13, 25, "Pendant of Ice");
            AddItem(13, 26, "Pendant of Wind");
            AddItem(13, 27, "Pendant of Water");
            AddItem(13, 28, "Pendant of Ability");
            AddItem(13, 38, "Moonstone Pendant");
            AddItem(13, 39, "Elite Transfer Skeleton Ring");
            AddItem(13, 40, "Jack Olantern Transformation Ring");
            AddItem(13, 41, "Christmas Transformation Ring");
            AddItem(13, 42, "Game Master Transformation Ring");
            AddItem(13, 68, "Snowman Transformation Ring");
            AddItem(13, 76, "Panda Transformation Ring");
            AddItem(13, 122, "Skeleton Transformation Ring");
            AddItem(13, 163, "? Transformation Ring");
            AddItem(13, 164, "?? Transformation Ring");
            AddItem(13, 165, "??? Transformation Ring");

            // --- Misc.cs ---
            AddItem(13, 11, "Life Stone");
            AddItem(14, 90, "Golden Cherry Blossom Branch");
            AddItem(14, 28, "Lost Map");
            AddItem(14, 29, "Symbol of Kundun");

            // --- Orbs.cs ---
            AddItem(12, 7, "Orb of Twisting Slash");
            AddItem(12, 8, "Orb of Healing");
            AddItem(12, 9, "Orb of Greater Defense");
            AddItem(12, 10, "Orb of Greater Damage");
            AddItem(12, 11, "Orb of Summoning");
            AddItem(12, 12, "Orb of Rageful Blow");
            AddItem(12, 13, "Orb of Impale");
            AddItem(12, 14, "Orb of Greater Fortitude");
            AddItem(12, 16, "Orb of Fire Slash");
            AddItem(12, 17, "Orb of Penetration");
            AddItem(12, 18, "Orb of Ice Arrow");
            AddItem(12, 19, "Orb of Death Stab");
            AddItem(12, 44, "Crystal of Destruction");
            AddItem(12, 45, "Crystal of Multi-Shot");
            AddItem(12, 46, "Crystal of Recovery");
            AddItem(12, 47, "Crystal of Flame Strike");

            // DL Scrolls (Group 12 according to Orbs.cs)
            AddItem(12, 21, "Scroll of FireBurst");
            AddItem(12, 22, "Scroll of Summon");
            AddItem(12, 23, "Scroll of Critical Damage");
            AddItem(12, 24, "Scroll of Electric Spark");
            AddItem(12, 35, "Scroll of Fire Scream");
            AddItem(12, 48, "Scroll of Chaotic Diseier");

            // --- PackedJewels.cs ---
            AddItem(12, 30, "Packed Jewel of Bless");
            AddItem(12, 31, "Packed Jewel of Soul");
            AddItem(12, 136, "Packed Jewel of Life");
            AddItem(12, 137, "Packed Jewel of Creation");
            AddItem(12, 138, "Packed Jewel of Guardian");
            AddItem(12, 139, "Packed Gemstone");
            AddItem(12, 140, "Packed Jewel of Harmony");
            AddItem(12, 141, "Packed Jewel of Chaos");
            AddItem(12, 142, "Packed Lower refine stone");
            AddItem(12, 143, "Packed Higher refine stone");

            // --- Pets.cs ---
            AddItem(13, 0, "Guardian Angel");
            AddItem(13, 1, "Imp");
            AddItem(13, 2, "Horn of Uniria");
            AddItem(13, 3, "Horn of Dinorant");
            AddItem(13, 4, "Dark Horse");
            AddItem(13, 5, "Dark Raven");
            AddItem(13, 37, "Horn of Fenrir");
            AddItem(13, 64, "Demon");
            AddItem(13, 65, "Spirit of Guardian");
            AddItem(13, 67, "Pet Rudolf");
            AddItem(13, 80, "Pet Panda");
            AddItem(13, 106, "Pet Unicorn");
            AddItem(13, 123, "Pet Skeleton");

            // Pet crafting items (Group 13)
            AddItem(13, 31, "Spirit");
            AddItem(13, 32, "Splinter of Armor");
            AddItem(13, 33, "Bless of Guardian");
            AddItem(13, 34, "Claw of Beast");
            AddItem(13, 35, "Fragment of Horn");
            AddItem(13, 36, "Broken Horn");

            // --- Potions.cs ---
            AddItem(14, 0, "Apple");
            AddItem(14, 1, "Small Healing Potion");
            AddItem(14, 2, "Medium Healing Potion");
            AddItem(14, 3, "Large Healing Potion");
            AddItem(14, 4, "Small Mana Potion");
            AddItem(14, 5, "Medium Mana Potion");
            AddItem(14, 6, "Large Mana Potion");
            AddItem(14, 35, "Small Shield Potion");
            AddItem(14, 36, "Medium Shield Potion");
            AddItem(14, 37, "Large Shield Potion");
            AddItem(14, 38, "Small Complex Potion");
            AddItem(14, 39, "Medium Complex Potion");
            AddItem(14, 40, "Large Complex Potion");
            AddItem(14, 9, "Ale");
            AddItem(14, 8, "Antidote");
            AddItem(14, 10, "Town Portal Scroll");
            AddItem(13, 15, "Fruits"); // Group 13 according to code
            AddItem(14, 7, "Potion of Bless"); // Compound name
            AddItem(14, 46, "Jack O'Lantern Blessings");
            AddItem(14, 47, "Jack O'Lantern Wrath");
            AddItem(14, 48, "Jack O'Lantern Cry");
            AddItem(14, 49, "Jack O'Lantern Food");
            AddItem(14, 50, "Jack O'Lantern Drink");
            AddItem(14, 85, "Cherry Blossom Wine");
            AddItem(14, 86, "Cherry Blossom Rice Cake");
            AddItem(14, 87, "Cherry Blossom Flower Petal");

            // --- Quest.cs ---
            AddItem(14, 23, "Scroll of Emperor");
            AddItem(14, 24, "Broken Sword");
            AddItem(14, 25, "Tear of Elf");
            AddItem(14, 26, "Soul Shard of Wizard");
            AddItem(14, 68, "Eye of Abyssal");
            AddItem(14, 65, "Flame of Death Beam Knight");
            AddItem(14, 66, "Horn of Hell Maine");
            AddItem(14, 67, "Feather of Dark Phoenix");

            // --- Scrolls.cs ---
            AddItem(15, 0, "Scroll of Poison");
            AddItem(15, 1, "Scroll of Meteorite");
            AddItem(15, 2, "Scroll of Lighting");
            AddItem(15, 3, "Scroll of Fire Ball");
            AddItem(15, 4, "Scroll of Flame");
            AddItem(15, 5, "Scroll of Teleport");
            AddItem(15, 6, "Scroll of Ice");
            AddItem(15, 7, "Scroll of Twister");
            AddItem(15, 8, "Scroll of Evil Spirit");
            AddItem(15, 9, "Scroll of Hellfire");
            AddItem(15, 10, "Scroll of Power Wave");
            AddItem(15, 11, "Scroll of Aqua Beam");
            AddItem(15, 12, "Scroll of Cometfall");
            AddItem(15, 13, "Scroll of Inferno");
            AddItem(15, 14, "Scroll of Teleport Ally");
            AddItem(15, 15, "Scroll of Soul Barrier");
            AddItem(15, 16, "Scroll of Decay");
            AddItem(15, 17, "Scroll of Ice Storm");
            AddItem(15, 18, "Scroll of Nova");
            AddItem(15, 19, "Chain Lightning Parchment");
            AddItem(15, 20, "Drain Life Parchment");
            AddItem(15, 21, "Lightning Shock Parchment");
            AddItem(15, 22, "Damage Reflection Parchment");
            AddItem(15, 23, "Berserker Parchment");
            AddItem(15, 24, "Sleep Parchment");
            AddItem(15, 26, "Weakness Parchment");
            AddItem(15, 27, "Innovation Parchment");
            AddItem(15, 28, "Scroll of Wizardry Enhance");
            AddItem(15, 29, "Scroll of Gigantic Storm");
            AddItem(15, 30, "Chain Drive Parchment");
            AddItem(15, 31, "Dark Side Parchment");
            AddItem(15, 32, "Dragon Roar Parchment");
            AddItem(15, 33, "Dragon Slasher Parchment");
            AddItem(15, 34, "Ignore Defense Parchment");
            AddItem(15, 35, "Increase Health Parchment");
            AddItem(15, 36, "Increase Block Parchment");

            // --- SocketSystem.cs ---
            AddItem(12, 60, "Seed (Fire)");
            AddItem(12, 61, "Seed (Water)");
            AddItem(12, 62, "Seed (Ice)");
            AddItem(12, 63, "Seed (Wind)");
            AddItem(12, 64, "Seed (Lightning)");
            AddItem(12, 65, "Seed (Earth)");
            AddItem(12, 70, "Sphere (Mono)");
            AddItem(12, 71, "Sphere (Di)");
            AddItem(12, 72, "Sphere (Tri)");
            AddItem(12, 73, "Sphere (4)");
            AddItem(12, 74, "Sphere (5)");
            for (byte level = 0; level < 5; level++)
            {
                AddItem(12, (short)(100 + (level * 6) + 0), $"Seed Sphere (Fire) ({level + 1})");
                AddItem(12, (short)(100 + (level * 6) + 1), $"Seed Sphere (Water) ({level + 1})");
                AddItem(12, (short)(100 + (level * 6) + 2), $"Seed Sphere (Ice) ({level + 1})");
                AddItem(12, (short)(100 + (level * 6) + 3), $"Seed Sphere (Wind) ({level + 1})");
                AddItem(12, (short)(100 + (level * 6) + 4), $"Seed Sphere (Lightning) ({level + 1})");
                AddItem(12, (short)(100 + (level * 6) + 5), $"Seed Sphere (Earth) ({level + 1})");
            }

            // --- Weapons.cs ---
            // Group 0: Swords / Gloves
            AddItem(0, 0, "Kris");
            AddItem(0, 1, "Short Sword");
            AddItem(0, 2, "Rapier");
            AddItem(0, 3, "Katache"); // Katana
            AddItem(0, 4, "Sword of Assassin");
            AddItem(0, 5, "Blade");
            AddItem(0, 6, "Gladius");
            AddItem(0, 7, "Falchion");
            AddItem(0, 8, "Serpent Sword");
            AddItem(0, 9, "Sword of Salamander");
            AddItem(0, 10, "Light Saber");
            AddItem(0, 11, "Legendary Sword");
            AddItem(0, 12, "Heliacal Sword");
            AddItem(0, 13, "Double Blade");
            AddItem(0, 14, "Lighting Sword");
            AddItem(0, 15, "Giant Sword");
            AddItem(0, 16, "Sword of Destruction");
            AddItem(0, 17, "Dark Breaker");
            AddItem(0, 18, "Thunder Blade");
            AddItem(0, 19, "Divine Sword of Archangel");
            AddItem(0, 20, "Knight Blade");
            AddItem(0, 21, "Dark Reign Blade");
            AddItem(0, 22, "Bone Blade");
            AddItem(0, 23, "Explosion Blade");
            AddItem(0, 24, "Daybreak");
            AddItem(0, 25, "Sword Dancer");
            AddItem(0, 26, "Flamberge");
            AddItem(0, 27, "Sword Breaker");
            AddItem(0, 28, "Imperial Sword");
            AddItem(0, 31, "Rune Blade");
            AddItem(0, 32, "Sacred Glove");
            AddItem(0, 33, "Storm Hard Glove");
            AddItem(0, 34, "Piercing Blade Glove");
            AddItem(0, 35, "Phoenix Soul Star");

            // Group 1: Axes
            AddItem(1, 0, "Small Axe");
            AddItem(1, 1, "Hand Axe");
            AddItem(1, 2, "Double Axe");
            AddItem(1, 3, "Tomahawk");
            AddItem(1, 4, "Elven Axe");
            AddItem(1, 5, "Battle Axe");
            AddItem(1, 6, "Nikkea Axe"); // Nikea Axe
            AddItem(1, 7, "Larkan Axe");
            AddItem(1, 8, "Crescent Axe");

            // Group 2: Maces / Scepters
            AddItem(2, 0, "Mace");
            AddItem(2, 1, "Morning Star");
            AddItem(2, 2, "Flail");
            AddItem(2, 3, "Great Hammer"); // Great Warhammer
            AddItem(2, 4, "Crystal Morning Star");
            AddItem(2, 5, "Crystal Sword"); // Despite "Sword", it's in group 2
            AddItem(2, 6, "Chaos Dragon Axe"); // Despite "Axe", it's in group 2
            AddItem(2, 7, "Elemental Mace");
            AddItem(2, 8, "Battle Scepter");
            AddItem(2, 9, "Master Scepter");
            AddItem(2, 10, "Great Scepter");
            AddItem(2, 11, "Lord Scepter");
            AddItem(2, 12, "Great Lord Scepter");
            AddItem(2, 13, "Divine Scepter of Archangel");
            AddItem(2, 14, "Soleil Scepter");
            AddItem(2, 15, "Shining Scepter");
            AddItem(2, 16, "Frost Mace");
            AddItem(2, 17, "Absolute Scepter");
            AddItem(2, 18, "Stryker Scepter");

            // Group 3: Spears
            AddItem(3, 0, "Light Spear");
            AddItem(3, 1, "Spear");
            AddItem(3, 2, "Dragon Lance");
            AddItem(3, 3, "Giant Trident"); // Great Trident
            AddItem(3, 4, "Serpent Spear");
            AddItem(3, 5, "Double Poleaxe");
            AddItem(3, 6, "Halberd");
            AddItem(3, 7, "Berdysh");
            AddItem(3, 8, "Great Scythe");
            AddItem(3, 9, "Bill of Balrog");
            AddItem(3, 10, "Dragon Spear");
            AddItem(3, 11, "Beuroba");

            // Group 4: Bows / Crossbows / Ammunition
            AddItem(4, 0, "Short Bow");
            AddItem(4, 1, "Bow");
            AddItem(4, 2, "Elven Bow");
            AddItem(4, 3, "Battle Bow");
            AddItem(4, 4, "Tiger Bow");
            AddItem(4, 5, "Silver Bow");
            AddItem(4, 6, "Chaos Nature Bow");
            AddItem(4, 7, "Bolt"); // Ammunition
            AddItem(4, 8, "Crossbow");
            AddItem(4, 9, "Golden Crossbow");
            AddItem(4, 10, "Arquebus");
            AddItem(4, 11, "Light Crossbow");
            AddItem(4, 12, "Serpent Crossbow");
            AddItem(4, 13, "Bluewing Crossbow");
            AddItem(4, 14, "Aquagold Crossbow");
            AddItem(4, 15, "Arrows"); // Ammunition
            AddItem(4, 16, "Saint Crossbow");
            AddItem(4, 17, "Celestial Bow");
            AddItem(4, 18, "Divine Crossbow of Archangel");
            AddItem(4, 19, "Great Reign Crossbow");
            AddItem(4, 20, "Arrow Viper Bow");
            AddItem(4, 21, "Sylph Wind Bow");
            AddItem(4, 22, "Albatross Bow");
            AddItem(4, 23, "Stinger Bow");
            AddItem(4, 24, "Air Lyn Bow");

            // Group 5: Staffs / Sticks / Books
            AddItem(5, 0, "Skull Staff");
            AddItem(5, 1, "Angelic Staff");
            AddItem(5, 2, "Serpent Staff");
            AddItem(5, 3, "Thunder Staff"); // Lightning Staff
            AddItem(5, 4, "Gorgon Staff");
            AddItem(5, 5, "Legendary Staff");
            AddItem(5, 6, "Staff of Resurrection");
            AddItem(5, 7, "Chaos Lightning Staff");
            AddItem(5, 8, "Staff of Destruction");
            AddItem(5, 9, "Dragon Soul Staff");
            AddItem(5, 10, "Divine Staff of Archangel");
            AddItem(5, 11, "Staff of Kundun");
            AddItem(5, 12, "Grand Viper Staff");
            AddItem(5, 13, "Platina Staff");
            AddItem(5, 14, "Mistery Stick");
            AddItem(5, 15, "Violent Wind Stick");
            AddItem(5, 16, "Red Wing Stick");
            AddItem(5, 17, "Ancient Stick");
            AddItem(5, 18, "Demonic Stick"); // Black Rose Stick
            AddItem(5, 19, "Storm Blitz Stick"); // Aura Stick
            AddItem(5, 20, "Eternal Wing Stick");
            AddItem(5, 21, "Book of Sahamutt");
            AddItem(5, 22, "Book of Neil");
            AddItem(5, 23, "Book of Lagle");
            AddItem(5, 30, "Deadly Staff");
            AddItem(5, 31, "Imperial Staff");
            AddItem(5, 33, "Chromatic Staff");
            AddItem(5, 34, "Raven Stick");
            AddItem(5, 36, "Divine Stick of Archangel");

            // --- Wings.cs ---
            AddItem(12, 0, "Wings of Elf");
            AddItem(12, 1, "Wings of Heaven");
            AddItem(12, 2, "Wings of Satan");
            AddItem(12, 41, "Wings of Curse");
            AddItem(12, 3, "Wings of Spirits");
            AddItem(12, 4, "Wings of Soul");
            AddItem(12, 5, "Wings of Dragon");
            AddItem(12, 6, "Wings of Darkness");
            AddItem(12, 42, "Wings of Despair");
            AddItem(12, 49, "Cape of Fighter");
            AddItem(13, 30, "Cape of Lord"); // Group 13 according to code
            AddItem(13, 14, "Loch's Feather");
            AddItem(13, 53, "Feather of Condor");
            AddItem(13, 52, "Flame of Condor");
            AddItem(12, 36, "Wing of Storm");
            AddItem(12, 37, "Wing of Eternal");
            AddItem(12, 38, "Wing of Illusion");
            AddItem(12, 39, "Wing of Ruin");
            AddItem(12, 40, "Cape of Emperor");
            AddItem(12, 43, "Wing of Dimension");
            AddItem(12, 50, "Cape of Overrule");

            // --- END OF PASTED DATA ---

            return data;
        }

        /// <summary>
        /// Gets the item name based on its group and ID (number).
        /// </summary>
        /// <param name="group">The item group.</param>
        /// <param name="id">The item ID (number) within the group.</param>
        /// <returns>The item name, or null if not found.</returns>
        public static string GetItemName(byte group, short id)
        {
            if (Items.TryGetValue(group, out var groupDict))
            {
                if (groupDict.TryGetValue(id, out var name))
                {
                    return name;
                }
            }
            return null; // Or throw an exception, or return a default name
        }

        /// <summary>
        /// Gets the item name based on its ItemData (ReadOnlySpan<byte>).
        /// Assumes a standard OpenMU format where ID and Group can be extracted.
        /// Adjust indices based on actual packet structure for different versions if needed.
        /// </summary>
        /// <param name="itemData">The item data.</param>
        /// <returns>The item name, or null if not found or data is invalid.</returns>
        public static string GetItemName(ReadOnlySpan<byte> itemData)
        {
            // Basic validation: Need at least ID (byte 0) and Group (byte 5)
            if (itemData.Length < 6)
            {
                // Log or handle error: ItemData too short
                // Console.WriteLine($"[GetItemName] Error: ItemData too short ({itemData.Length} bytes), at least 6 required.");
                return null;
            }

            try
            {
                short id = itemData[0]; // Item ID is usually at the first byte
                byte groupAndHarmonyByte = itemData[5]; // Group info often in byte 5
                byte group = (byte)(groupAndHarmonyByte >> 4); // Extract group from upper 4 bits

                return GetItemName(group, id);
            }
            catch (IndexOutOfRangeException)
            {
                // Handle cases where itemData might be shorter than expected despite initial check
                // Console.WriteLine($"[GetItemName] Error: Index out of range accessing ItemData (Length: {itemData.Length}).");
                return null;
            }
        }
    }
}