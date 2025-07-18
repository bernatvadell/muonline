using System;
using MUnique.OpenMU.Network.Packets; // For CharacterClassNumber

namespace Client.Main.Models
{
    /// <summary>
    /// Represents the appearance of wings based on the byte array data.
    /// Wings are defined by their level (1, 2, 3) and their type within that level.
    /// </summary>
    public readonly struct WingAppearance
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WingAppearance"/> struct.
        /// </summary>
        /// <param name="level">The level of the wings (1, 2, 3) or 0 if none.</param>
        /// <param name="type">The type of the wings within their level.</param>
        public WingAppearance(byte level, byte type)
        {
            this.Level = level;
            this.Type = type;
        }

        /// <summary>
        /// Gets the level of the wings (1, 2, or 3). A value of 0 means no wings.
        /// Read from byte 5 (bits 2-3).
        /// </summary>
        public byte Level { get; }

        /// <summary>
        /// Gets the type of the wings within their level.
        /// Read from byte 9 (bits 0-2).
        /// </summary>
        public byte Type { get; }

        /// <summary>
        /// Gets a value indicating whether the character has any wings equipped.
        /// </summary>
        public bool HasWings => this.Level > 0;
    }

    /// <summary>
    /// Represents the parsed appearance data of a character, including equipped items, wings, and pets.
    /// This structure is based on the byte array format described in the Appearance.md documentation.
    /// </summary>
    public readonly struct AppearanceData
    {
        private readonly ReadOnlyMemory<byte> _rawData;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppearanceData"/> struct.
        /// </summary>
        /// <param name="rawData">The raw byte array containing the appearance data.</param>
        public AppearanceData(ReadOnlyMemory<byte> rawData)
        {
            _rawData = rawData;
        }

        /// <summary>
        /// Gets the raw appearance data.
        /// </summary>
        public ReadOnlySpan<byte> RawData => _rawData.Span;

        // ------------------------------------------------------------------
        // Internal helpers for item levels
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the 24‑bit combined glow‐level index packed in bytes 6–8.
        /// </summary>
        private int LevelIndex => RawData.Length >= 9
            ? (RawData[6] << 16) | (RawData[7] << 8) | RawData[8]
            : 0;

        /// <summary>
        /// Converts a 3‑bit glow level (0–7) to the actual item level (0, 1, 3, 5, …).
        /// </summary>
        private static byte ConvertGlowToItemLevel(byte glow) =>
            glow == 0 ? (byte)0 : (byte)((glow - 1) * 2 + 1);

        /// <summary>
        /// Gets the item level for the specified slot index (0=LeftHand, 1=RightHand, …, 6=Boots).
        /// </summary>
        private byte GetItemLevel(int slotIndex)
        {
            if (RawData.Length < 9)
                return 0;

            byte glow = (byte)((LevelIndex >> (slotIndex * 3)) & 0x7);
            return ConvertGlowToItemLevel(glow);
        }

        // ------------------------------------------------------------------
        // Character class, pose, and item indices
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the character class. (Byte 0, bits 4-7)
        /// </summary>
        public CharacterClassNumber CharacterClass => RawData.Length > 0
            ? (CharacterClassNumber)((RawData[0] >> 4) & 0xF)
            : CharacterClassNumber.DarkWizard;

        /// <summary>
        /// Gets the character pose. (Byte 0, bits 0-3)
        /// </summary>
        public byte CharacterPose => RawData.Length > 0
            ? (byte)(RawData[0] & 0xF)
            : (byte)0;

        /// <summary>
        /// Gets the left hand item index. (Byte 1)
        /// </summary>
        public byte LeftHandItemIndex => RawData.Length > 1 ? RawData[1] : (byte)0xFF;

        /// <summary>
        /// Gets the right hand item index. (Byte 2)
        /// </summary>
        public byte RightHandItemIndex => RawData.Length > 2 ? RawData[2] : (byte)0xFF;

        /// <summary>
        /// Gets the helm item index (lower 4 bits from Byte 3, 5th bit from Byte 9, 6-9th bits from Byte 13).
        /// </summary>
        public short HelmItemIndex
        {
            get
            {
                if (RawData.Length < 14) return 0xFF;
                byte lower4 = (byte)((RawData[3] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 7) & 0x1);
                byte upper4 = (byte)(RawData[13] & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the armor item index (lower 4 bits from Byte 3, 5th bit from Byte 9, 6-9th bits from Byte 14).
        /// </summary>
        public short ArmorItemIndex
        {
            get
            {
                if (RawData.Length < 15) return 0xFF;
                byte lower4 = (byte)(RawData[3] & 0xF);
                byte bit5 = (byte)((RawData[9] >> 6) & 0x1);
                byte upper4 = (byte)((RawData[14] >> 4) & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the pants item index (lower 4 bits from Byte 4, 5th bit from Byte 9, 6-9th bits from Byte 14).
        /// </summary>
        public short PantsItemIndex
        {
            get
            {
                if (RawData.Length < 15) return 0xFF;
                byte lower4 = (byte)((RawData[4] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 5) & 0x1);
                byte upper4 = (byte)(RawData[14] & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the gloves item index (lower 4 bits from Byte 4, 5th bit from Byte 9, 6-9th bits from Byte 15).
        /// </summary>
        public short GlovesItemIndex
        {
            get
            {
                if (RawData.Length < 16) return 0xFF;
                byte lower4 = (byte)(RawData[4] & 0xF);
                byte bit5 = (byte)((RawData[9] >> 4) & 0x1);
                byte upper4 = (byte)((RawData[15] >> 4) & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the boots item index (lower 4 bits from Byte 5, 5th bit from Byte 9, 6-9th bits from Byte 15).
        /// </summary>
        public short BootsItemIndex
        {
            get
            {
                if (RawData.Length < 16) return 0xFF;
                byte lower4 = (byte)((RawData[5] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 3) & 0x1);
                byte upper4 = (byte)(RawData[15] & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        // ------------------------------------------------------------------
        // Corrected item level properties
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the left hand item level.
        /// </summary>
        public byte LeftHandItemLevel => GetItemLevel(0);

        /// <summary>
        /// Gets the right hand item level.
        /// </summary>
        public byte RightHandItemLevel => GetItemLevel(1);

        /// <summary>
        /// Gets the helm item level.
        /// </summary>
        public byte HelmItemLevel => GetItemLevel(2);

        /// <summary>
        /// Gets the armor item level.
        /// </summary>
        public byte ArmorItemLevel => GetItemLevel(3);

        /// <summary>
        /// Gets the pants item level.
        /// </summary>
        public byte PantsItemLevel => GetItemLevel(4);

        /// <summary>
        /// Gets the gloves item level.
        /// </summary>
        public byte GlovesItemLevel => GetItemLevel(5);

        /// <summary>
        /// Gets the boots item level.
        /// </summary>
        public byte BootsItemLevel => GetItemLevel(6);

        // ------------------------------------------------------------------
        // Wings, pets, and flags
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the complete wing information for the character.
        /// </summary>
        public WingAppearance WingInfo
        {
            get
            {
                if (RawData.Length < 10)
                {
                    return new WingAppearance(0, 0);
                }

                byte wingLevelBits = (byte)((RawData[5] >> 2) & 0x3);
                byte wingType = (byte)(RawData[9] & 0x7);

                if (wingLevelBits == 0 || wingType == 0)
                {
                    return new WingAppearance(0, 0);
                }

                return new WingAppearance(wingLevelBits, wingType);
            }
        }

        /// <summary>
        /// Gets the pet item index. (Byte 16, bits 2-7)
        /// </summary>
        public byte PetItemIndex => RawData.Length > 16 ? (byte)((RawData[16] >> 2) & 0x3F) : (byte)0;

        /// <summary>
        /// Gets the small wing item index. (Byte 17, bits 4-7)
        /// </summary>
        public byte SmallWingItemIndex => RawData.Length > 17 ? (byte)((RawData[17] >> 4) & 0xF) : (byte)0;

        /// <summary>
        /// Gets the helm excellent option flag. (Byte 10, bit 7)
        /// </summary>
        public bool HelmExcellent => RawData.Length > 10 && ((RawData[10] >> 7) & 0x1) == 1;

        public bool ArmorExcellent => RawData.Length > 10 && ((RawData[10] >> 6) & 0x1) == 1;
        public bool PantsExcellent => RawData.Length > 10 && ((RawData[10] >> 5) & 0x1) == 1;
        public bool GlovesExcellent => RawData.Length > 10 && ((RawData[10] >> 4) & 0x1) == 1;
        public bool BootsExcellent => RawData.Length > 10 && ((RawData[10] >> 3) & 0x1) == 1;
        public bool LeftHandExcellent => RawData.Length > 10 && ((RawData[10] >> 2) & 0x1) == 1;
        public bool RightHandExcellent => RawData.Length > 10 && ((RawData[10] >> 1) & 0x1) == 1;

        /// <summary>
        /// Gets the helm ancient option flag. (Byte 11, bit 7)
        /// </summary>
        public bool HelmAncient => RawData.Length > 11 && ((RawData[11] >> 7) & 0x1) == 1;

        public bool ArmorAncient => RawData.Length > 11 && ((RawData[11] >> 6) & 0x1) == 1;
        public bool PantsAncient => RawData.Length > 11 && ((RawData[11] >> 5) & 0x1) == 1;
        public bool GlovesAncient => RawData.Length > 11 && ((RawData[11] >> 4) & 0x1) == 1;
        public bool BootsAncient => RawData.Length > 11 && ((RawData[11] >> 3) & 0x1) == 1;
        public bool LeftHandAncient => RawData.Length > 11 && ((RawData[11] >> 2) & 0x1) == 1;
        public bool RightHandAncient => RawData.Length > 11 && ((RawData[11] >> 1) & 0x1) == 1;
        public bool FullAncientSet => RawData.Length > 11 && (RawData[11] & 0x1) == 1;

        /// <summary>
        /// Gets the left hand item group. (Byte 12, bits 5-7)
        /// </summary>
        public byte LeftHandItemGroup => RawData.Length > 12 ? (byte)((RawData[12] >> 5) & 0x7) : (byte)0x7;

        /// <summary>
        /// Gets the right hand item group. (Byte 13, bits 5-7)
        /// </summary>
        public byte RightHandItemGroup => RawData.Length > 13 ? (byte)((RawData[13] >> 5) & 0x7) : (byte)0x7;

        /// <summary>
        /// Gets the Dinorant flag. (Byte 10, bit 0)
        /// </summary>
        public bool HasDinorant => RawData.Length > 10 && (RawData[10] & 0x1) == 1;

        /// <summary>
        /// Gets the Fenrir flag. (Byte 12, bit 2)
        /// </summary>
        public bool HasFenrir => RawData.Length > 12 && ((RawData[12] >> 2) & 0x1) == 1;

        /// <summary>
        /// Gets the Dark Horse flag. (Byte 12, bit 0)
        /// </summary>
        public bool HasDarkHorse => RawData.Length > 12 && (RawData[12] & 0x1) == 1;

        /// <summary>
        /// Gets the Blue Fenrir flag. (Byte 16, bit 1)
        /// </summary>
        public bool HasBlueFenrir => RawData.Length > 16 && ((RawData[16] >> 1) & 0x1) == 1;

        /// <summary>
        /// Gets the Black Fenrir flag. (Byte 16, bit 0)
        /// </summary>
        public bool HasBlackFenrir => RawData.Length > 16 && (RawData[16] & 0x1) == 1;

        /// <summary>
        /// Gets the Gold Fenrir flag. (Byte 17, bit 0)
        /// </summary>
        public bool HasGoldFenrir => RawData.Length > 17 && (RawData[17] & 0x1) == 1;
    }
}
