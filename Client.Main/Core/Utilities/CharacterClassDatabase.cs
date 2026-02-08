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
        /// Based on the 18-byte appearance format from Appearance.md:
        /// - Byte 0, bits 4-7 (upper 4 bits): Character class (4 bits = values 0-15)
        /// - Byte 0, bits 0-3 (lower 4 bits): Character pose
        ///
        /// The 4-bit class value represents the base class (0-6) with evolution level,
        /// which needs to be mapped to CharacterClassNumber enum values.
        /// </summary>
        /// <param name="appearanceData">The appearance data span (18 or 27 bytes).</param>
        /// <param name="classNumber">The parsed class number.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public static bool TryParseClassFromAppearance(ReadOnlySpan<byte> appearanceData, out CharacterClassNumber classNumber)
        {
            classNumber = CharacterClassNumber.DarkWizard; // Default
            if (appearanceData.IsEmpty || appearanceData.Length < 1)
            {
                return false; // Not enough data
            }

            // According to Appearance.md:
            // Byte 0, bits 4-7 (upper 4 bits) = Character class
            // The 4-bit value encodes both base class and evolution level
            byte classByte = appearanceData[0];
            int rawClassValue = (classByte >> 4) & 0x0F; // Upper 4 bits (0-15)

            // Map the 4-bit rendering class to CharacterClassNumber
            // The 4-bit encoding is based on original client CLASS_TYPE:
            // 0=DW, 1=DK, 2=Elf, 3=MG, 4=DL, 5=Sum, 6=RF + evolution bits
            //
            // Mapping for standard OpenMU/S6:
            // Bits 0-2: Base class (0-6)
            // Bit 3: Evolution flag (0=base/1st, 1=2nd/3rd)
            //
            // However, for better compatibility, we try to map known values directly:
            classNumber = rawClassValue switch
            {
                // Base classes (1st job)
                0 => CharacterClassNumber.DarkWizard,      // 0b0000
                1 => CharacterClassNumber.DarkKnight,      // 0b0001
                2 => CharacterClassNumber.FairyElf,        // 0b0010
                3 => CharacterClassNumber.MagicGladiator,  // 0b0011
                4 => CharacterClassNumber.DarkLord,        // 0b0100
                5 => CharacterClassNumber.Summoner,        // 0b0101
                6 => CharacterClassNumber.RageFighter,     // 0b0110

                // 2nd classes (with evolution bit set)
                8 => CharacterClassNumber.SoulMaster,      // 0b1000 (DW 2nd)
                9 => CharacterClassNumber.BladeKnight,     // 0b1001 (DK 2nd)
                10 => CharacterClassNumber.MuseElf,        // 0b1010 (Elf 2nd)
                11 => CharacterClassNumber.DuelMaster,     // 0b1011 (MG 2nd) - Note: MG only has 3rd
                12 => CharacterClassNumber.LordEmperor,    // 0b1100 (DL 2nd) - Note: DL only has 3rd
                13 => CharacterClassNumber.BloodySummoner, // 0b1101 (Sum 2nd)
                14 => CharacterClassNumber.FistMaster,     // 0b1110 (RF 2nd) - Note: RF only has 3rd

                // 3rd classes (alternative encoding if used)
                15 => CharacterClassNumber.GrandMaster,    // 0b1111 - might be used for 3rd

                _ => CharacterClassNumber.DarkWizard       // Default fallback
            };

            return true;
        }

        /// <summary>
        /// Maps a raw 4-bit appearance class value to a CharacterClassNumber.
        /// This is a more lenient version that also handles SERVER_CLASS_TYPE values
        /// which may be directly stored in some packet formats.
        /// </summary>
        /// <param name="rawValue">The raw class value (4 or 5 bits).</param>
        /// <returns>The mapped CharacterClassNumber.</returns>
        public static CharacterClassNumber MapRawClassToEnum(int rawValue)
        {
            // First try direct mapping if it's a valid SERVER_CLASS_TYPE value
            return rawValue switch
            {
                // SERVER_CLASS_TYPE values (used in CharacterList packets)
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

                // Fallback for 4-bit appearance class (rendering class)
                1 => CharacterClassNumber.DarkKnight,   // CLASS_KNIGHT
                5 => CharacterClassNumber.Summoner,     // CLASS_SUMMONER
                9 => CharacterClassNumber.BladeKnight,  // 2nd DK in 4-bit
                14 => CharacterClassNumber.FistMaster,  // 2nd RF in 4-bit
                15 => CharacterClassNumber.GrandMaster, // 3rd class flag

                _ => CharacterClassNumber.DarkWizard    // Default fallback
            };
        }
    }
}