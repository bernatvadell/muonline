using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Client.Data.BMD
{
    public static class SkillBMDReader
    {
        private const int MaxSkills = 1024;
        private const int RecordSize = 88;
        private const int NameLength = 32;
        private const ushort ChecksumKey = 0x5A18;

        private static readonly byte[] BuxCode = { 0xFC, 0xCF, 0xAB };

        public static Dictionary<int, SkillBMD> LoadSkills(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Skill file not found: {filePath}", filePath);
            }

            var buffer = File.ReadAllBytes(filePath);
            if (buffer.Length < RecordSize)
            {
                throw new InvalidDataException($"Skill file '{filePath}' is too small ({buffer.Length} bytes).");
            }

            var skills = new Dictionary<int, SkillBMD>();

            var dataLength = Math.Min(buffer.Length, RecordSize * MaxSkills);
            var dataSpan = buffer.AsSpan(0, dataLength);

            if (buffer.Length >= RecordSize * MaxSkills + sizeof(uint))
            {
                var storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(
                    buffer.AsSpan(RecordSize * MaxSkills, sizeof(uint)));
                var computedChecksum = GenerateChecksum(dataSpan);

                if (storedChecksum != 0 && storedChecksum != computedChecksum)
                {
                    throw new InvalidDataException(
                        $"Skill file checksum mismatch. stored=0x{storedChecksum:X8}, computed=0x{computedChecksum:X8}");
                }
            }

            var recordCount = Math.Min(MaxSkills, dataLength / RecordSize);
            Span<byte> decrypted = stackalloc byte[RecordSize];

            for (var index = 0; index < recordCount; index++)
            {
                var recordSpan = dataSpan.Slice(index * RecordSize, RecordSize);
                DecryptRecord(recordSpan, decrypted);

                var skill = ParseSkill(decrypted);
                if (!string.IsNullOrWhiteSpace(skill.Name))
                {
                    skills[index] = skill;
                }
            }

            return skills;
        }

        private static void DecryptRecord(ReadOnlySpan<byte> encrypted, Span<byte> destination)
        {
            for (var i = 0; i < encrypted.Length; i++)
            {
                destination[i] = (byte)(encrypted[i] ^ BuxCode[i % BuxCode.Length]);
            }
        }

        private static SkillBMD ParseSkill(ReadOnlySpan<byte> record)
        {
            var skill = new SkillBMD();

            var nameSpan = record.Slice(0, NameLength);
            var nullIndex = nameSpan.IndexOf((byte)0);
            if (nullIndex < 0)
            {
                nullIndex = NameLength;
            }

            skill.Name = Encoding.UTF8.GetString(nameSpan.Slice(0, nullIndex)).TrimEnd();

            var offset = NameLength;

            skill.RequiredLevel = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.Damage = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.ManaCost = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.AbilityGaugeCost = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.Distance = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(offset, sizeof(uint)));
            offset += sizeof(uint);

            skill.Delay = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.RequiredEnergy = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.RequiredLeadership = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.MasteryType = record[offset++];
            skill.SkillUseType = record[offset++];

            skill.SkillBrand = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(offset, sizeof(uint)));
            offset += sizeof(uint);

            skill.KillCount = record[offset++];

            record.Slice(offset, SkillBMD.MaxDutyClass).CopyTo(skill.RequireDutyClass);
            offset += SkillBMD.MaxDutyClass;

            record.Slice(offset, SkillBMD.MaxClass).CopyTo(skill.RequireClass);
            offset += SkillBMD.MaxClass;

            skill.SkillRank = record[offset++];

            skill.MagicIcon = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            var typeValue = record[offset++];
            skill.Type = typeValue <= (byte)TypeSkill.FriendlySkill
                ? (TypeSkill)typeValue
                : TypeSkill.None;

            offset++; // padding byte before Strength

            skill.RequiredStrength = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.RequiredDexterity = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.ItemSkill = record[offset++];
            skill.IsDamage = record[offset++] != 0;
            skill.Effect = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));

            return skill;
        }

        private static uint GenerateChecksum(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < sizeof(uint))
            {
                return 0;
            }

            var key = (uint)ChecksumKey;
            var result = key << 9;

            for (var offset = 0; offset <= buffer.Length - sizeof(uint); offset += sizeof(uint))
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, sizeof(uint)));
                var index = (offset / sizeof(uint)) + ChecksumKey;

                if ((index & 1) == 0)
                {
                    result ^= value;
                }
                else
                {
                    result += value;
                }

                if ((offset % 16) == 0)
                {
                    result ^= (uint)((key + result) >> (((offset / sizeof(uint)) % 8) + 1));
                }
            }

            return result;
        }
    }
}
