using System;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Helpers for parsing extended (5-15 bytes) MU item data packets used by Season 6 clients.
    /// </summary>
    internal static class ItemDataParser
    {
        public const int ExtendedItemMinLength = 5;
        public const int ExtendedItemMaxLength = 15;

        [Flags]
        internal enum ItemOptionFlags : byte
        {
            None = 0x00,
            HasOption = 0x01,
            HasLuck = 0x02,
            HasSkill = 0x04,
            HasExcellent = 0x08,
            HasAncient = 0x10,
            HasHarmony = 0x20,
            HasGuardian = 0x40,
            HasSockets = 0x80,
        }

        internal readonly struct ExtendedItemData
        {
            public readonly byte Group;
            public readonly short Number;
            public readonly byte Level;
            public readonly byte Durability;
            public readonly byte OptionLevel;
            public readonly byte OptionType;
            public readonly byte ExcellentFlags;
            public readonly byte AncientDiscriminator;
            public readonly byte AncientBonusOption;
            public readonly byte HarmonyOption;
            public readonly byte SocketBonusOption;
            public readonly byte SocketCount;
            public readonly bool HasLuck;
            public readonly bool HasSkill;
            public readonly bool HasOption;
            public readonly bool HasExcellent;
            public readonly bool HasAncient;
            public readonly bool HasHarmony;
            public readonly bool HasGuardian;
            public readonly bool HasSockets;
            public readonly int Length;

            public ExtendedItemData(
                byte group,
                short number,
                byte level,
                byte durability,
                byte optionLevel,
                byte optionType,
                byte excellentFlags,
                byte ancientDiscriminator,
                byte ancientBonusOption,
                byte harmonyOption,
                byte socketBonusOption,
                byte socketCount,
                ItemOptionFlags flags,
                int length)
            {
                Group = group;
                Number = number;
                Level = level;
                Durability = durability;
                OptionLevel = optionLevel;
                OptionType = optionType;
                ExcellentFlags = excellentFlags;
                AncientDiscriminator = ancientDiscriminator;
                AncientBonusOption = ancientBonusOption;
                HarmonyOption = harmonyOption;
                SocketBonusOption = socketBonusOption;
                SocketCount = socketCount;
                HasLuck = flags.HasFlag(ItemOptionFlags.HasLuck);
                HasSkill = flags.HasFlag(ItemOptionFlags.HasSkill);
                HasOption = flags.HasFlag(ItemOptionFlags.HasOption);
                HasExcellent = flags.HasFlag(ItemOptionFlags.HasExcellent);
                HasAncient = flags.HasFlag(ItemOptionFlags.HasAncient);
                HasHarmony = flags.HasFlag(ItemOptionFlags.HasHarmony);
                HasGuardian = flags.HasFlag(ItemOptionFlags.HasGuardian);
                HasSockets = flags.HasFlag(ItemOptionFlags.HasSockets);
                Length = length;
            }
        }

        public static bool TryGetExtendedItemLength(ReadOnlySpan<byte> data, out int length)
        {
            length = 0;
            if (data.Length < ExtendedItemMinLength)
            {
                return false;
            }

            var flags = (ItemOptionFlags)data[4];
            int size = ExtendedItemMinLength;

            if (flags.HasFlag(ItemOptionFlags.HasOption))
            {
                size++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasExcellent))
            {
                size++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasAncient))
            {
                size++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasHarmony))
            {
                size++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasSockets))
            {
                if (data.Length <= size)
                {
                    return false;
                }

                byte socketCount = (byte)(data[size] & 0x0F);
                size++;
                size += socketCount;
            }

            if (size < ExtendedItemMinLength || size > ExtendedItemMaxLength)
            {
                return false;
            }

            length = size;
            return true;
        }

        public static bool TryParseExtendedItemData(ReadOnlySpan<byte> data, out ExtendedItemData item)
        {
            item = default;
            if (data.Length < ExtendedItemMinLength)
            {
                return false;
            }

            byte group = (byte)((data[0] >> 4) & 0x0F);
            short number = (short)(((data[0] & 0x0F) << 8) | data[1]);
            byte level = data[2];
            byte durability = data[3];
            var flags = (ItemOptionFlags)data[4];

            int offset = ExtendedItemMinLength;
            byte optionLevel = 0;
            byte optionType = 0;
            byte excellentFlags = 0;
            byte ancientDiscriminator = 0;
            byte ancientBonusOption = 0;
            byte harmonyOption = 0;
            byte socketBonusOption = 0;
            byte socketCount = 0;

            if (flags.HasFlag(ItemOptionFlags.HasOption))
            {
                if (data.Length <= offset)
                {
                    return false;
                }

                byte opt = data[offset];
                optionLevel = (byte)(opt & 0x0F);
                optionType = (byte)((opt >> 4) & 0x0F);
                offset++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasExcellent))
            {
                if (data.Length <= offset)
                {
                    return false;
                }

                excellentFlags = data[offset];
                offset++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasAncient))
            {
                if (data.Length <= offset)
                {
                    return false;
                }

                byte anc = data[offset];
                ancientDiscriminator = (byte)(anc & 0x0F);
                ancientBonusOption = (byte)((anc >> 4) & 0x0F);
                offset++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasHarmony))
            {
                if (data.Length <= offset)
                {
                    return false;
                }

                harmonyOption = data[offset];
                offset++;
            }

            if (flags.HasFlag(ItemOptionFlags.HasSockets))
            {
                if (data.Length <= offset)
                {
                    return false;
                }

                byte socketInfo = data[offset];
                socketBonusOption = (byte)((socketInfo >> 4) & 0x0F);
                socketCount = (byte)(socketInfo & 0x0F);
                offset++;

                if (data.Length < offset + socketCount)
                {
                    return false;
                }

                offset += socketCount;
            }

            if (offset < ExtendedItemMinLength || offset > ExtendedItemMaxLength)
            {
                return false;
            }

            item = new ExtendedItemData(
                group,
                number,
                level,
                durability,
                optionLevel,
                optionType,
                excellentFlags,
                ancientDiscriminator,
                ancientBonusOption,
                harmonyOption,
                socketBonusOption,
                socketCount,
                flags,
                offset);
            return true;
        }

        public static bool TryGetDurability(ReadOnlySpan<byte> data, out byte durability)
        {
            if (TryParseExtendedItemData(data, out var ext) && ext.Length <= data.Length)
            {
                durability = ext.Durability;
                return true;
            }

            if (data.Length > 2)
            {
                durability = data[2];
                return true;
            }

            durability = 0;
            return false;
        }

        public static bool TrySetDurability(Span<byte> data, byte durability)
        {
            if (TryParseExtendedItemData(data, out var ext) && ext.Length <= data.Length)
            {
                if (data.Length > 3)
                {
                    data[3] = durability;
                    return true;
                }
            }
            else if (data.Length > 2)
            {
                data[2] = durability;
                return true;
            }

            return false;
        }

        public static bool TryGetExcellentFlags(ReadOnlySpan<byte> data, out byte flags)
        {
            if (TryParseExtendedItemData(data, out var ext) && ext.Length <= data.Length)
            {
                flags = ext.ExcellentFlags;
                return true;
            }

            if (data.Length > 3)
            {
                flags = data[3];
                return true;
            }

            flags = 0;
            return false;
        }

        public static bool TryGetGroupAndNumber(ReadOnlySpan<byte> data, out byte group, out short number)
        {
            group = 0;
            number = 0;

            if (TryParseExtendedItemData(data, out var ext) && ext.Length <= data.Length)
            {
                group = ext.Group;
                number = ext.Number;
                return true;
            }

            if (data.Length >= 6)
            {
                group = (byte)(data[5] >> 4);
                number = data[0];
                return true;
            }

            return false;
        }
    }
}
