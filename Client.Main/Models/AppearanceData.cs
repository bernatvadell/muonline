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
        public WingAppearance(byte level, byte type, short itemIndex)
        {
            this.Level = level;
            this.Type = type;
            this.ItemIndex = itemIndex;
        }

        public short ItemIndex { get; } = -1;

        /// <summary>
        /// Gets the level of the wings (1, 2, or 3). A value of 0 means no wings.
        /// </summary>
        public byte Level { get; }

        /// <summary>
        /// Gets the type of the wings within their level.
        /// </summary>
        public byte Type { get; }

        /// <summary>
        /// Gets a value indicating whether the character has any wings equipped.
        /// </summary>
        public bool HasWings => this.Level > 0 || this.ItemIndex >= 0;
    }

    /// <summary>
    /// Defines the format of the appearance data.
    /// </summary>
    public enum AppearanceFormat
    {
        /// <summary>
        /// Unknown or empty format.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Standard 18-byte appearance format (used in scope/spawn packets).
        /// Contains class in byte 0 bits 4-7 and complex bit-packed item indices.
        /// </summary>
        Standard18Byte = 18,

        /// <summary>
        /// Extended 25-byte equipment format (used in CharacterList Extended from SourceMain5.2).
        /// Contains 3-byte chunks for each equipment slot, no class embedded.
        /// Structure: RightHand(3) + LeftHand(3) + Helm(3) + Armor(3) + Pants(3) + Gloves(3) + Boots(3) + Wings(2) + Helper(2)
        /// </summary>
        Extended25Byte = 25,

        /// <summary>
        /// Extended 27-byte appearance format (used in some CharacterListExtended packets).
        /// Similar to 25-byte but with 2 additional bytes for extra flags.
        /// </summary>
        Extended27Byte = 27
    }

    /// <summary>
    /// Represents the parsed appearance data of a character, including equipped items, wings, and pets.
    /// Supports both the standard 18-byte format (Appearance.md) and the extended 25/27-byte format
    /// (SourceMain5.2 CharacterList Extended packets).
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
        /// Gets the detected format of the appearance data.
        /// </summary>
        public AppearanceFormat Format
        {
            get
            {
                if (_rawData.IsEmpty || _rawData.Length == 0) return AppearanceFormat.Unknown;
                if (_rawData.Length >= 27) return AppearanceFormat.Extended27Byte;
                if (_rawData.Length >= 25) return AppearanceFormat.Extended25Byte;
                if (_rawData.Length >= 18) return AppearanceFormat.Standard18Byte;
                return AppearanceFormat.Unknown;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is the extended 25/27-byte format.
        /// </summary>
        public bool IsExtendedFormat => Format == AppearanceFormat.Extended25Byte || Format == AppearanceFormat.Extended27Byte;

        // ------------------------------------------------------------------
        // Extended format parsing helpers (25/27-byte SourceMain5.2 format)
        // Structure per equipment slot (3 bytes):
        //   Byte 0: Low 4 bits = item number high nibble, High 4 bits = item group
        //   Byte 1: Item number low byte
        //   Byte 2: Bit 2 = ancient, Bit 3 = excellent, Bits 4-7 = glow level
        // ------------------------------------------------------------------

        private const byte HelperItemGroup = 13;
        private const short HelperDinorantNumber = 3;
        private const short HelperDarkHorseNumber = 4;
        private const short HelperFenrirNumber = 37;
        private const short MaxItemIndex = 512;

        private static (short Index, byte Group, byte Level, bool Excellent, bool Ancient) ParseExtendedSlot(ReadOnlySpan<byte> data, int offset)
        {
            if (data.Length < offset + 3)
                return (-1, 0xFF, 0, false, false);

            byte b0 = data[offset];
            byte b1 = data[offset + 1];
            byte b2 = data[offset + 2];

            // Check for empty slot (0xFF markers)
            if (b0 == 0xFF && b1 == 0xFF)
                return (-1, 0xFF, 0, false, false);

            // SourceMain5.2: MAKEWORD(Equipment[offset + 1], Equipment[offset] & 0x0F)
            // => item number = low byte (b1) + high nibble bits from b0
            short itemNumber = (short)(b1 | ((b0 & 0x0F) << 8));
            byte group = (byte)((b0 >> 4) & 0x0F);

            // Parse flags from byte 2
            bool ancient = (b2 & 0x04) != 0;     // Bit 2
            bool excellent = (b2 & 0x08) != 0;   // Bit 3
            byte glowLevel = (byte)((b2 >> 4) & 0x0F); // Bits 4-7

            // SourceMain5.2 applies LevelConvert(glowLevel), where only 0..7 are valid,
            // and values above 7 are treated as level 0.
            byte convertedLevel = ConvertGlowToItemLevel(glowLevel);

            return (itemNumber, group, convertedLevel, excellent, ancient);
        }

        private static (short Index, byte Group) ParseExtendedWings(ReadOnlySpan<byte> data, int offset)
        {
            if (data.Length < offset + 2)
                return (-1, 0xFF);

            byte b0 = data[offset];
            byte b1 = data[offset + 1];

            if (b0 == 0xFF && b1 == 0xFF)
                return (-1, 0xFF);

            // Wings use a different 2-byte packing in SourceMain5.2:
            // number = Equipment[offset + 1] + ((Equipment[offset] & 0x0F) << 4)
            // Group: (b0 >> 4) & 0x0F
            short itemNumber = (short)(b1 + ((b0 & 0x0F) << 4));
            byte group = (byte)((b0 >> 4) & 0x0F);

            return (itemNumber, group);
        }

        private static (short ItemNumber, byte Group, byte Variant) ParseExtendedHelper(ReadOnlySpan<byte> data, int offset)
        {
            if (data.Length < offset + 2)
                return (-1, 0xFF, 0);

            byte b0 = data[offset];
            byte b1 = data[offset + 1];
            if (b0 == 0xFF && b1 == 0xFF)
                return (-1, 0xFF, 0);

            // SourceMain5.2:
            // number = b1 + ((b0 & 0x0F) << 8);
            // itemNumber = number & (MAX_ITEM_INDEX - 1)
            short number = (short)(b1 | ((b0 & 0x0F) << 8));
            short itemNumber = (short)(number & (MaxItemIndex - 1));
            byte group = (byte)((b0 >> 4) & 0x0F);
            byte variant = (byte)((b0 & 0x0E) >> 1);
            return (itemNumber, group, variant);
        }

        // ------------------------------------------------------------------
        // Standard 18-byte format parsing helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the 24‑bit combined glow‐level index packed in bytes 6–8 (18-byte format only).
        /// </summary>
        private int LevelIndex18 => !IsExtendedFormat && RawData.Length >= 9
            ? (RawData[6] << 16) | (RawData[7] << 8) | RawData[8]
            : 0;

        /// <summary>
        /// Converts a 3‑bit glow level (0–7) to the actual item level.
        /// </summary>
        private static byte ConvertGlowToItemLevel(byte glow)
        {
            return glow switch
            {
                0 => 0,
                1 => 3,
                2 => 5,
                3 => 7,
                4 => 9,
                5 => 11,
                6 => 13,
                7 => 15,
                _ => 0
            };
        }

        /// <summary>
        /// Gets the item level for the specified slot index (18-byte format).
        /// </summary>
        private byte GetItemLevel18(int slotIndex)
        {
            if (IsExtendedFormat || RawData.Length < 9)
                return 0;

            byte glow = (byte)((LevelIndex18 >> (slotIndex * 3)) & 0x7);
            return ConvertGlowToItemLevel(glow);
        }

        // ------------------------------------------------------------------
        // Character class (only valid for 18-byte format)
        // In extended format, class comes from the packet's explicit field
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the character class from appearance data.
        /// Only valid for 18-byte format where class is in byte 0 bits 4-7.
        /// For extended format, use the explicit class from the CharacterList packet.
        /// </summary>
        public CharacterClassNumber CharacterClass
        {
            get
            {
                if (RawData.Length == 0) return CharacterClassNumber.DarkWizard;

                if (IsExtendedFormat)
                {
                    // Extended format doesn't contain class - return default
                    // The class should be obtained from the packet's explicit field
                    return CharacterClassNumber.DarkWizard;
                }

                // 18-byte format: class in byte 0 bits 4-7
                return (CharacterClassNumber)((RawData[0] >> 4) & 0xF);
            }
        }

        /// <summary>
        /// Gets the character pose (18-byte format only, byte 0 bits 0-3).
        /// </summary>
        public byte CharacterPose => !IsExtendedFormat && RawData.Length > 0
            ? (byte)(RawData[0] & 0xF)
            : (byte)0;

        // ------------------------------------------------------------------
        // Weapon indices
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the left hand (Weapon 1) item index.
        /// </summary>
        public byte LeftHandItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Left hand at offset 3-5
                    var slot = ParseExtendedSlot(RawData, 3);
                    return slot.Index < 0 ? (byte)0xFF : unchecked((byte)slot.Index);
                }
                // 18-byte: Byte 1
                return RawData.Length > 1 ? RawData[1] : (byte)0xFF;
            }
        }

        /// <summary>
        /// Gets the left hand (Weapon 1) item number.
        /// </summary>
        public short LeftHandItemNumber
        {
            get
            {
                if (IsExtendedFormat)
                {
                    return ParseExtendedSlot(RawData, 3).Index;
                }

                return RawData.Length > 1 ? RawData[1] : (short)0xFF;
            }
        }

        /// <summary>
        /// Gets the right hand (Weapon 2) item index.
        /// </summary>
        public byte RightHandItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Right hand at offset 0-2
                    var slot = ParseExtendedSlot(RawData, 0);
                    return slot.Index < 0 ? (byte)0xFF : unchecked((byte)slot.Index);
                }
                // 18-byte: Byte 2
                return RawData.Length > 2 ? RawData[2] : (byte)0xFF;
            }
        }

        /// <summary>
        /// Gets the right hand (Weapon 2) item number.
        /// </summary>
        public short RightHandItemNumber
        {
            get
            {
                if (IsExtendedFormat)
                {
                    return ParseExtendedSlot(RawData, 0).Index;
                }

                return RawData.Length > 2 ? RawData[2] : (short)0xFF;
            }
        }

        /// <summary>
        /// Gets the left hand item group.
        /// </summary>
        public byte LeftHandItemGroup
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 3);
                    return slot.Group;
                }
                // 18-byte: Byte 12, bits 5-7
                return RawData.Length > 12 ? (byte)((RawData[12] >> 5) & 0x7) : (byte)0x7;
            }
        }

        /// <summary>
        /// Gets the right hand item group.
        /// </summary>
        public byte RightHandItemGroup
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 0);
                    return slot.Group;
                }
                // 18-byte: Byte 13, bits 5-7
                return RawData.Length > 13 ? (byte)((RawData[13] >> 5) & 0x7) : (byte)0x7;
            }
        }

        // ------------------------------------------------------------------
        // Armor indices (Helm, Armor, Pants, Gloves, Boots)
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the helm item index.
        /// </summary>
        public short HelmItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Helm at offset 6-8
                    var slot = ParseExtendedSlot(RawData, 6);
                    return slot.Index;
                }
                // 18-byte format
                if (RawData.Length < 14) return 0xFF;
                byte lower4 = (byte)((RawData[3] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 7) & 0x1);
                byte upper4 = (byte)(RawData[13] & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the armor item index.
        /// </summary>
        public short ArmorItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Armor at offset 9-11
                    var slot = ParseExtendedSlot(RawData, 9);
                    return slot.Index;
                }
                // 18-byte format
                if (RawData.Length < 15) return 0xFF;
                byte lower4 = (byte)(RawData[3] & 0xF);
                byte bit5 = (byte)((RawData[9] >> 6) & 0x1);
                byte upper4 = (byte)((RawData[14] >> 4) & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the pants item index.
        /// </summary>
        public short PantsItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Pants at offset 12-14
                    var slot = ParseExtendedSlot(RawData, 12);
                    return slot.Index;
                }
                // 18-byte format
                if (RawData.Length < 15) return 0xFF;
                byte lower4 = (byte)((RawData[4] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 5) & 0x1);
                byte upper4 = (byte)(RawData[14] & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the gloves item index.
        /// </summary>
        public short GlovesItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Gloves at offset 15-17
                    var slot = ParseExtendedSlot(RawData, 15);
                    return slot.Index;
                }
                // 18-byte format
                if (RawData.Length < 16) return 0xFF;
                byte lower4 = (byte)(RawData[4] & 0xF);
                byte bit5 = (byte)((RawData[9] >> 4) & 0x1);
                byte upper4 = (byte)((RawData[15] >> 4) & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        /// <summary>
        /// Gets the boots item index.
        /// </summary>
        public short BootsItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Boots at offset 18-20
                    var slot = ParseExtendedSlot(RawData, 18);
                    return slot.Index;
                }
                // 18-byte format
                if (RawData.Length < 16) return 0xFF;
                byte lower4 = (byte)((RawData[5] >> 4) & 0xF);
                byte bit5 = (byte)((RawData[9] >> 3) & 0x1);
                byte upper4 = (byte)(RawData[15] & 0xF);
                return (short)(lower4 | (bit5 << 4) | (upper4 << 5));
            }
        }

        // ------------------------------------------------------------------
        // Item levels
        // ------------------------------------------------------------------

        public byte LeftHandItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 3);
                    return slot.Level;
                }
                return GetItemLevel18(0);
            }
        }

        public byte RightHandItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 0);
                    return slot.Level;
                }
                return GetItemLevel18(1);
            }
        }

        public byte HelmItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 6);
                    return slot.Level;
                }
                return GetItemLevel18(2);
            }
        }

        public byte ArmorItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 9);
                    return slot.Level;
                }
                return GetItemLevel18(3);
            }
        }

        public byte PantsItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 12);
                    return slot.Level;
                }
                return GetItemLevel18(4);
            }
        }

        public byte GlovesItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 15);
                    return slot.Level;
                }
                return GetItemLevel18(5);
            }
        }

        public byte BootsItemLevel
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var slot = ParseExtendedSlot(RawData, 18);
                    return slot.Level;
                }
                return GetItemLevel18(6);
            }
        }

        // ------------------------------------------------------------------
        // Excellent flags
        // ------------------------------------------------------------------

        public bool HelmExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 6).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 7) & 0x1) == 1;
            }
        }

        public bool ArmorExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 9).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 6) & 0x1) == 1;
            }
        }

        public bool PantsExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 12).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 5) & 0x1) == 1;
            }
        }

        public bool GlovesExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 15).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 4) & 0x1) == 1;
            }
        }

        public bool BootsExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 18).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 3) & 0x1) == 1;
            }
        }

        public bool LeftHandExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 3).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 2) & 0x1) == 1;
            }
        }

        public bool RightHandExcellent
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 0).Excellent;
                return RawData.Length > 10 && ((RawData[10] >> 1) & 0x1) == 1;
            }
        }

        // ------------------------------------------------------------------
        // Ancient flags
        // ------------------------------------------------------------------

        public bool HelmAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 6).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 7) & 0x1) == 1;
            }
        }

        public bool ArmorAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 9).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 6) & 0x1) == 1;
            }
        }

        public bool PantsAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 12).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 5) & 0x1) == 1;
            }
        }

        public bool GlovesAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 15).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 4) & 0x1) == 1;
            }
        }

        public bool BootsAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 18).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 3) & 0x1) == 1;
            }
        }

        public bool LeftHandAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 3).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 2) & 0x1) == 1;
            }
        }

        public bool RightHandAncient
        {
            get
            {
                if (IsExtendedFormat) return ParseExtendedSlot(RawData, 0).Ancient;
                return RawData.Length > 11 && ((RawData[11] >> 1) & 0x1) == 1;
            }
        }

        public bool FullAncientSet => !IsExtendedFormat && RawData.Length > 11 && (RawData[11] & 0x1) == 1;

        // ------------------------------------------------------------------
        // Wings, pets, and mounts
        // ------------------------------------------------------------------

        /// <summary>
        /// Gets the wing information for the character.
        /// </summary>
        public WingAppearance WingInfo
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Wings at offset 21-22
                    var wing = ParseExtendedWings(RawData, 21);
                    if (wing.Index < 0)
                        return new WingAppearance(0, 0);

                    // Map wing index to level/type for compatibility
                    // In extended format we have the actual item index
                    return new WingAppearance(1, 0, wing.Index);
                }

                // 18-byte format
                if (RawData.Length < 10)
                    return new WingAppearance(0, 0);

                byte wingLevelBits = (byte)((RawData[5] >> 2) & 0x3);
                byte wingType = (byte)(RawData[9] & 0x7);

                if (wingLevelBits == 0 || wingType == 0)
                    return new WingAppearance(0, 0);

                return new WingAppearance(wingLevelBits, wingType);
            }
        }

        /// <summary>
        /// Gets the pet/helper item index.
        /// </summary>
        public byte PetItemIndex
        {
            get
            {
                if (IsExtendedFormat)
                {
                    // Extended: Helper at offset 23-24
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.ItemNumber < 0 ? (byte)0 : unchecked((byte)helper.ItemNumber);
                }
                // 18-byte: Byte 16, bits 2-7
                return RawData.Length > 16 ? (byte)((RawData[16] >> 2) & 0x3F) : (byte)0;
            }
        }

        public byte SmallWingItemIndex => !IsExtendedFormat && RawData.Length > 17 ? (byte)((RawData[17] >> 4) & 0xF) : (byte)0;

        public bool HasDinorant
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.Group == HelperItemGroup && helper.ItemNumber == HelperDinorantNumber;
                }
                return RawData.Length > 10 && (RawData[10] & 0x1) == 1;
            }
        }

        public bool HasFenrir
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.Group == HelperItemGroup && helper.ItemNumber == HelperFenrirNumber;
                }
                return RawData.Length > 12 && ((RawData[12] >> 2) & 0x1) == 1;
            }
        }

        public bool HasDarkHorse
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.Group == HelperItemGroup && helper.ItemNumber == HelperDarkHorseNumber;
                }
                return RawData.Length > 12 && (RawData[12] & 0x1) == 1;
            }
        }

        public bool HasBlueFenrir
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.Group == HelperItemGroup
                        && helper.ItemNumber == HelperFenrirNumber
                        && helper.Variant == 2;
                }

                return RawData.Length > 16 && ((RawData[16] >> 1) & 0x1) == 1;
            }
        }

        public bool HasBlackFenrir
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.Group == HelperItemGroup
                        && helper.ItemNumber == HelperFenrirNumber
                        && helper.Variant == 1;
                }

                return RawData.Length > 16 && (RawData[16] & 0x1) == 1;
            }
        }

        public bool HasGoldFenrir
        {
            get
            {
                if (IsExtendedFormat)
                {
                    var helper = ParseExtendedHelper(RawData, 23);
                    return helper.Group == HelperItemGroup
                        && helper.ItemNumber == HelperFenrirNumber
                        && helper.Variant == 3;
                }

                return RawData.Length > 17 && (RawData[17] & 0x1) == 1;
            }
        }
    }
}
