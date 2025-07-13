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

        /// <summary>
        /// Gets the character class. (Byte 0, bits 4-7)
        /// </summary>
        public CharacterClassNumber CharacterClass => RawData.Length > 0
            ? (CharacterClassNumber)((RawData[0] >> 4) & 0xF)
            : CharacterClassNumber.DarkWizard; // Default or unknown

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
        /// Gets the helm item index (lower 4 bits from Byte 3, 5th bit from Byte 9, 6-9th bit from Byte 13).
        /// </summary>
        public short HelmItemIndex
        {
            get
            {
                if (RawData.Length < 14) return 0xFF;
                byte lower4 = (byte)((RawData[3] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 7) & 0x1);
                byte upper4 = (byte)(RawData[13] & 0xF);
                return (byte)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the armor item index (lower 4 bits from Byte 3, 5th bit from Byte 9, 6-9th bit from Byte 14).
        /// </summary>
        public short ArmorItemIndex
        {
            get
            {
                if (RawData.Length < 15) return 0xFF;
                byte lower4 = (byte)(RawData[3] & 0xF);
                byte bit5 = (byte)((RawData[9] >> 6) & 0x1);
                byte upper4 = (byte)((RawData[14] >> 4) & 0xF);
                return (byte)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the pants item index (lower 4 bits from Byte 4, 5th bit from Byte 9, 6-9th bit from Byte 14).
        /// </summary>
        public short PantsItemIndex
        {
            get
            {
                if (RawData.Length < 15) return 0xFF;
                byte lower4 = (byte)((RawData[4] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 5) & 0x1);
                byte upper4 = (byte)(RawData[14] & 0xF);
                return (byte)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the gloves item index (lower 4 bits from Byte 4, 5th bit from Byte 9, 6-9th bit from Byte 15).
        /// </summary>
        public short GlovesItemIndex
        {
            get
            {
                if (RawData.Length < 16) return 0xFF;
                byte lower4 = (byte)(RawData[4] & 0xF);
                byte bit5 = (byte)((RawData[9] >> 4) & 0x1);
                byte upper4 = (byte)((RawData[15] >> 4) & 0xF);
                return (byte)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the boots item index (lower 4 bits from Byte 5, 5th bit from Byte 9, 6-9th bit from Byte 15).
        /// </summary>
        public short BootsItemIndex
        {
            get
            {
                if (RawData.Length < 16) return 0xFF;
                byte lower4 = (byte)((RawData[5] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 3) & 0x1);
                byte upper4 = (byte)(RawData[15] & 0xF);
                return (byte)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the left hand item level. (Byte 6, bits 5-7)
        /// </summary>
        public byte LeftHandItemLevel => RawData.Length > 6 ? (byte)((RawData[6] >> 5) & 0x7) : (byte)0;

        /// <summary>
        /// Gets the right hand item level. (Byte 6, bits 2-4)
        /// </summary>
        public byte RightHandItemLevel => RawData.Length > 6 ? (byte)((RawData[6] >> 2) & 0x7) : (byte)0;

        /// <summary>
        /// Gets the helm item level. (Byte 6, bits 0-1 and Byte 7, bit 7)
        /// </summary>
        public byte HelmItemLevel => RawData.Length > 7 ? (byte)(((RawData[6] & 0x3) << 1) | ((RawData[7] >> 7) & 0x1)) : (byte)0;

        /// <summary>
        /// Gets the armor item level. (Byte 7, bits 4-6)
        /// </summary>
        public byte ArmorItemLevel => RawData.Length > 7 ? (byte)((RawData[7] >> 4) & 0x7) : (byte)0;

        /// <summary>
        /// Gets the pants item level. (Byte 7, bits 1-3)
        /// </summary>
        public byte PantsItemLevel => RawData.Length > 7 ? (byte)((RawData[7] >> 1) & 0x7) : (byte)0;

        /// <summary>
        /// Gets the gloves item level. (Byte 7, bit 0 and Byte 8, bits 6-7)
        /// </summary>
        public byte GlovesItemLevel => RawData.Length > 8 ? (byte)(((RawData[7] & 0x1) << 2) | ((RawData[8] >> 6) & 0x3)) : (byte)0;

        /// <summary>
        /// Gets the boots item level. (Byte 8, bits 3-5)
        /// </summary>
        public byte BootsItemLevel => RawData.Length > 8 ? (byte)((RawData[8] >> 3) & 0x7) : (byte)0;

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

                // Wing level from byte 5 (bits 2-3)
                byte wingLevelBits = (byte)((RawData[5] >> 2) & 0x3);

                // Wing type from byte 9 (bits 0-2)
                byte wingType = (byte)(RawData[9] & 0x7);

                if (wingLevelBits == 0 || wingType == 0)
                {
                    return new WingAppearance(0, 0); // No wings
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

        /// <summary>
        /// Gets the armor excellent option flag. (Byte 10, bit 6)
        /// </summary>
        public bool ArmorExcellent => RawData.Length > 10 && ((RawData[10] >> 6) & 0x1) == 1;

        /// <summary>
        /// Gets the pants excellent option flag. (Byte 10, bit 5)
        /// </summary>
        public bool PantsExcellent => RawData.Length > 10 && ((RawData[10] >> 5) & 0x1) == 1;

        /// <summary>
        /// Gets the gloves excellent option flag. (Byte 10, bit 4)
        /// </summary>
        public bool GlovesExcellent => RawData.Length > 10 && ((RawData[10] >> 4) & 0x1) == 1;

        /// <summary>
        /// Gets the boots excellent option flag. (Byte 10, bit 3)
        /// </summary>
        public bool BootsExcellent => RawData.Length > 10 && ((RawData[10] >> 3) & 0x1) == 1;

        /// <summary>
        /// Gets the left hand item excellent option flag. (Byte 10, bit 2)
        /// </summary>
        public bool LeftHandExcellent => RawData.Length > 10 && ((RawData[10] >> 2) & 0x1) == 1;

        /// <summary>
        /// Gets the right hand item excellent option flag. (Byte 10, bit 1)
        /// </summary>
        public bool RightHandExcellent => RawData.Length > 10 && ((RawData[10] >> 1) & 0x1) == 1;

        /// <summary>
        /// Gets the helm ancient option flag. (Byte 11, bit 7)
        /// </summary>
        public bool HelmAncient => RawData.Length > 11 && ((RawData[11] >> 7) & 0x1) == 1;

        /// <summary>
        /// Gets the armor ancient option flag. (Byte 11, bit 6)
        /// </summary>
        public bool ArmorAncient => RawData.Length > 11 && ((RawData[11] >> 6) & 0x1) == 1;

        /// <summary>
        /// Gets the pants ancient option flag. (Byte 11, bit 5)
        /// </summary>
        public bool PantsAncient => RawData.Length > 11 && ((RawData[11] >> 5) & 0x1) == 1;

        /// <summary>
        /// Gets the gloves ancient option flag. (Byte 11, bit 4)
        /// </summary>
        public bool GlovesAncient => RawData.Length > 11 && ((RawData[11] >> 4) & 0x1) == 1;

        /// <summary>
        /// Gets the boots ancient option flag. (Byte 11, bit 3)
        /// </summary>
        public bool BootsAncient => RawData.Length > 11 && ((RawData[11] >> 3) & 0x1) == 1;

        /// <summary>
        /// Gets the left hand item ancient option flag. (Byte 11, bit 2)
        /// </summary>
        public bool LeftHandAncient => RawData.Length > 11 && ((RawData[11] >> 2) & 0x1) == 1;

        /// <summary>
        /// Gets the right hand item ancient option flag. (Byte 11, bit 1)
        /// </summary>
        public bool RightHandAncient => RawData.Length > 11 && ((RawData[11] >> 1) & 0x1) == 1;

        /// <summary>
        /// Gets the full ancient set flag. (Byte 11, bit 0)
        /// </summary>
        public bool FullAncientSet => RawData.Length > 11 && (RawData[11] & 0x1) == 1;

        /// <summary>
        /// Gets the left hand item group. (Byte 12, bits 5-7)
        /// </summary>
        public byte LeftHandItemGroup => RawData.Length > 12 ? (byte)((RawData[12] >> 5) & 0x7) : (byte)0x7; // 0x7 = empty

        /// <summary>
        /// Gets the right hand item group. (Byte 13, bits 5-7)
        /// </summary>
        public byte RightHandItemGroup => RawData.Length > 13 ? (byte)((RawData[13] >> 5) & 0x7) : (byte)0x7; // 0x7 = empty

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