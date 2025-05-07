using System;
using System.Collections.Generic;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Provides mapping between character class numbers and names.
    /// </summary>
    public static class CharacterClassDatabase
    {
        private static readonly Dictionary<CharacterClassNumber, string> ClassNames = InitializeClassData();

        private static Dictionary<CharacterClassNumber, string> InitializeClassData()
        {
            // Populate the dictionary based on the CharacterClassNumber enum
            return new Dictionary<CharacterClassNumber, string>
            {
                { CharacterClassNumber.DarkWizard, "Dark Wizard" },
                { CharacterClassNumber.SoulMaster, "Soul Master" },
                { CharacterClassNumber.GrandMaster, "Grand Master" },
                { CharacterClassNumber.DarkKnight, "Dark Knight" },
                { CharacterClassNumber.BladeKnight, "Blade Knight" },
                { CharacterClassNumber.BladeMaster, "Blade Master" },
                { CharacterClassNumber.FairyElf, "Fairy Elf" },
                { CharacterClassNumber.MuseElf, "Muse Elf" },
                { CharacterClassNumber.HighElf, "High Elf" },
                { CharacterClassNumber.MagicGladiator, "Magic Gladiator" },
                { CharacterClassNumber.DuelMaster, "Duel Master" },
                { CharacterClassNumber.DarkLord, "Dark Lord" },
                { CharacterClassNumber.LordEmperor, "Lord Emperor" },
                { CharacterClassNumber.Summoner, "Summoner" },
                { CharacterClassNumber.BloodySummoner, "Bloody Summoner" },
                { CharacterClassNumber.DimensionMaster, "Dimension Master" },
                { CharacterClassNumber.RageFighter, "Rage Fighter" },
                { CharacterClassNumber.FistMaster, "Fist Master" }
                // Add any other classes if the enum expands
            };
        }

        /// <summary>
        /// Gets the name of the character class based on its number.
        /// </summary>
        /// <param name="classNumber">The character class number.</param>
        /// <returns>The name of the class, or the number as a string if not found.</returns>
        public static string GetClassName(CharacterClassNumber classNumber)
        {
            return ClassNames.TryGetValue(classNumber, out var name) ? name : classNumber.ToString();
        }

        /// <summary>
        /// Tries to parse the character class number from the appearance data.
        /// This depends heavily on the protocol version and appearance data structure.
        /// This is a common S6+ approach where class is in the upper bits of the first appearance byte.
        /// </summary>
        /// <param name="appearanceData">The appearance data span.</param>
        /// <param name="classNumber">The parsed class number.</param>
        /// <returns>True if parsing was potentially successful, false otherwise.</returns>
        public static bool TryParseClassFromAppearance(ReadOnlySpan<byte> appearanceData, out CharacterClassNumber classNumber)
        {
            classNumber = CharacterClassNumber.DarkWizard; // Default
            if (appearanceData.IsEmpty || appearanceData.Length < 1)
            {
                return false; // Not enough data
            }

            // Common S6+ approach: Class is in bits 3-7 of the first appearance byte
            // Example: byte[0] = 0bCCCH_GGGX (C=Class, H=Helm?, G=Glove?, X=?)
            // Class = (byte[0] >> 3) & 0b11111 (5 bits for class) - Adjust mask if needed!
            // This needs verification against specific protocol docs.
            // Let's assume a simpler older format for now: Class in upper nibble?
            // Example: byte[0] = 0bCCCC_XXXX (C=Class, X=Other flags)
            // Class = (byte[0] >> 4) & 0x0F;

            // Let's try the CharacterList packet structure's approach (bits 3-7 of byte 18/19)
            // This requires the *full* appearance data block.
            // Example for CharacterList.CharacterData (S6, 34 bytes total, appearance starts at index 15, length 18)
            // Appearance byte index 3 (relative to appearance start) = Packet byte index 18
            if (appearanceData.Length > 3) // Need at least 4 bytes in appearance block
            {
                byte classByte = appearanceData[3]; // This corresponds to packet byte 18 in CharacterList.CharacterData
                // Class is often encoded in bits 3-7 (5 bits)
                int classValue = (classByte >> 3) & 0b1_1111; // Mask for 5 bits

                // Map the raw value to the enum. This mapping might need adjustment!
                // This mapping is based on common server emulator patterns.
                classNumber = classValue switch
                {
                    0 => CharacterClassNumber.DarkWizard,
                    2 => CharacterClassNumber.SoulMaster,
                    3 => CharacterClassNumber.GrandMaster,

                    4 => CharacterClassNumber.DarkKnight,
                    6 => CharacterClassNumber.BladeKnight,
                    7 => CharacterClassNumber.BladeMaster,

                    8 => CharacterClassNumber.FairyElf,
                    10 => CharacterClassNumber.MuseElf,
                    11 => CharacterClassNumber.HighElf,

                    12 => CharacterClassNumber.MagicGladiator,
                    13 => CharacterClassNumber.DuelMaster,

                    16 => CharacterClassNumber.DarkLord,
                    17 => CharacterClassNumber.LordEmperor,

                    20 => CharacterClassNumber.Summoner,
                    22 => CharacterClassNumber.BloodySummoner,
                    23 => CharacterClassNumber.DimensionMaster,

                    24 => CharacterClassNumber.RageFighter,
                    25 => CharacterClassNumber.FistMaster,
                    _ => CharacterClassNumber.DarkWizard // Default fallback
                };
                return true; // Parsing attempted
            }


            return false; // Couldn't determine class
        }
    }
}