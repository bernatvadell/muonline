using System.Collections.Generic;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Provides mapping between NPC/Monster type numbers and their designations.
    /// </summary>
    public static class NpcDatabase
    {
        // Note: Monster numbers overlap with NPC numbers in this range.
        private static readonly Dictionary<ushort, string> NpcNames = InitializeNpcData();

        private static Dictionary<ushort, string> InitializeNpcData()
        {
            return new Dictionary<ushort, string>
            {
                // === Monsters (from provided map files) ===
                // Lorencia
                { 0, "Bull Fighter" },
                { 1, "Hound" },
                { 2, "Budge Dragon" },
                { 3, "Spider" },
                { 4, "Elite Bull Fighter" },
                { 6, "Lich" },
                { 7, "Giant" },
                { 14, "Skeleton Warrior" },
                // Dungeon
                { 5, "Hell Hound" },
                { 8, "Poison Bull" },
                { 9, "Thunder Lich" },
                { 10, "Dark Knight" },
                { 11, "Ghost" },
                { 12, "Larva" },
                { 13, "Hell Spider" },
                { 15, "Skeleton Archer" },
                { 16, "Elite Skeleton" },
                { 17, "Cyclops" },
                { 18, "Gorgon" },
                { 100, "Lance Trap" },
                { 101, "Iron Stick Trap" },
                { 102, "Fire Trap" },
                { 103, "Meteorite Trap" },
                // Devias
                { 19, "Yeti" },
                { 20, "Elite Yeti" },
                { 21, "Assassin" },
                { 22, "Ice Monster" },
                { 23, "Hommerd" },
                { 24, "Worm" },
                { 25, "Ice Queen" },
                // Noria
                { 26, "Goblin" },
                { 27, "Chain Scorpion" },
                { 28, "Beetle Monster" },
                { 29, "Hunter" },
                { 30, "Forest Monster" },
                { 31, "Agon" },
                { 32, "Stone Golem" },
                { 33, "Elite Goblin" },
                // Lost Tower
                { 34, "Cursed Wizard" },
                { 35, "Death Gorgon" },
                { 36, "Shadow" },
                { 37, "Devil" },
                { 38, "Balrog" },
                { 39, "Poison Shadow" },
                { 40, "Death Knight" },
                { 41, "Death Cow" },
                // Atlans
                { 45, "Bahamut" },
                { 46, "Vepar" },
                { 47, "Valkyrie" },
                { 48, "Lizard King" },
                { 49, "Hydra" },
                // { 50, "Sea Worm" }, // Seems identical to Hydra in 0.75? Check definition if different.
                { 51, "Great Bahamut" },
                { 52, "Silver Valkyrie" },
                // Tarkan
                { 57, "Iron Wheel" },
                { 58, "Tantallos" },
                { 59, "Zaikan" },
                { 60, "Bloody Wolf" },
                { 61, "Beam Knight" },
                { 62, "Mutant" },
                { 63, "Death Beam Knight" },
                { 64, "Orc Archer" },      // DS3
                { 65, "Elite Orc" },       // DS3
                { 66, "Cursed King" },     // DS4 Boss
                { 67, "Metal Balrog" },    // DS3 Boss
                // { 69, "Alquamos" },      // Not explicitly defined in 0.75 files provided
                { 70, "Queen Rainer" },    // Icarus
                { 71, "Mega Crust" },      // Icarus
                { 72, "Phantom Knight" },  // Icarus
                { 73, "Drakan" },          // Icarus
                { 74, "Alpha Crust" },     // Icarus
                { 75, "Great Drakan" },    // Icarus
                // { 76, "Dark Phoenix Shield" }, // Not defined in 0.75 files
                { 77, "Dark Phoenix" },    // Icarus
                // Add more monsters...

                // === NPCs (from NpcInitialization and map files) ===
                { 200, "Guard" }, // From Arena
                { 235, "Priest Sevina" },
                { 236, "Golden Archer" },
                { 237, "Charon" }, // Devil Square NPC
                { 238, "Chaos Goblin" },
                { 239, "Arena Guard" }, // From Arena
                { 240, "Sebina the Priestess" }, // Also Guard in Atlans/Dungeon? Name collision? Using Sebina.
                { 241, "Warehouse Keeper" },
                { 242, "Hanzo the Blacksmith" },
                { 243, "Wandering Merchant" },
                { 244, "Potion Girl Amy" },
                { 245, "Zienna the Weapons Merchant" },
                { 246, "Guild Master" },
                { 247, "Pasi the Mage" },
                { 248, "Lumen the Barmaid" },
                { 249, "Guard" },
                { 250, "Wandering Merchant" },
                { 251, "Elf Lala" },
                // { 252, "???" },
                { 253, "Alex the Weapons Merchant" }, // Also Wandering Merchant? Using Alex.
                { 254, "Wizard" }, // Potion Girl? Using Wizard designation.
                { 255, "Pasi the Mage" }, // Duplicate? (Noria)
                // Add more NPCs...
            };
        }

        /// <summary>
        /// Gets the name of the NPC or Monster based on its type number.
        /// </summary>
        /// <param name="npcTypeNumber">The NPC type number.</param>
        /// <returns>The name of the NPC/Monster, or a default string if not found.</returns>
        public static string GetNpcName(ushort npcTypeNumber)
        {
            return NpcNames.TryGetValue(npcTypeNumber, out var name) ? name : $"Unknown NPC/Monster ({npcTypeNumber})";
        }
    }
}