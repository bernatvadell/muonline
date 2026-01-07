using Client.Main.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Client.Main.Data
{
    public class MoveCommandDataManager
    {
        private const int MovereqRecordSize = 84;
        private static readonly byte[] MovereqXorKey = { 0xFC, 0xCF, 0xAB };

        private static MoveCommandDataManager _instance;
        public static MoveCommandDataManager Instance => _instance ??= new MoveCommandDataManager();

        private List<MoveCommandInfo> _moveCommands;

        private readonly struct MovereqRecord
        {
            public MovereqRecord(int index, string main, string sub, int requiredLevel, int requiredMaxLevel, int requiredZen, int gate)
            {
                Index = index;
                Main = main;
                Sub = sub;
                RequiredLevel = requiredLevel;
                RequiredMaxLevel = requiredMaxLevel;
                RequiredZen = requiredZen;
                Gate = gate;
            }

            public int Index { get; }
            public string Main { get; }
            public string Sub { get; }
            public int RequiredLevel { get; }
            public int RequiredMaxLevel { get; }
            public int RequiredZen { get; }
            public int Gate { get; }
        }

        private MoveCommandDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            if (!TryLoadFromMovereq(out _moveCommands))
            {
                _moveCommands = BuildMoveCommandInfos(DefaultMovereqRecords);
            }

            _moveCommands = _moveCommands
                .OrderBy(m => m.RequiredLevel)
                .ThenBy(m => m.DisplayName)
                .ToList();
        }

        public List<MoveCommandInfo> GetMoveCommandDataList()
        {
            return _moveCommands.ToList();
        }

        private static bool TryLoadFromMovereq(out List<MoveCommandInfo> moveCommands)
        {
            moveCommands = null;
            var assembly = typeof(MoveCommandDataManager).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            var resourceName = resourceNames.FirstOrDefault(name =>
                name.Contains(".Data.S6.", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith("movereq_eng.bmd", StringComparison.OrdinalIgnoreCase));
            resourceName ??= resourceNames.FirstOrDefault(name =>
                name.Contains(".Data.S6.", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("movereq_", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase));

            try
            {
                if (string.IsNullOrWhiteSpace(resourceName))
                {
                    return false;
                }

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return false;
                }

                var records = LoadMovereqRecords(stream);
                moveCommands = BuildMoveCommandInfos(records);
                return moveCommands.Count > 0;
            }
            catch
            {
                moveCommands = null;
                return false;
            }
        }

        private static List<MovereqRecord> LoadMovereqRecords(System.IO.Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            var count = reader.ReadInt32();
            var records = new List<MovereqRecord>(Math.Max(0, count));

            for (var i = 0; i < count; i++)
            {
                var buffer = reader.ReadBytes(MovereqRecordSize);
                if (buffer.Length != MovereqRecordSize)
                {
                    break;
                }

                ApplyMovereqXor(buffer);

                var index = BitConverter.ToInt32(buffer, 0);
                var main = ReadMovereqString(buffer, 4, 32);
                var sub = ReadMovereqString(buffer, 36, 32);
                var requiredLevel = BitConverter.ToInt32(buffer, 68);
                var requiredMaxLevel = BitConverter.ToInt32(buffer, 72);
                var requiredZen = BitConverter.ToInt32(buffer, 76);
                var gate = BitConverter.ToInt32(buffer, 80);

                records.Add(new MovereqRecord(index, main, sub, requiredLevel, requiredMaxLevel, requiredZen, gate));
            }

            return records;
        }

        private static List<MoveCommandInfo> BuildMoveCommandInfos(IEnumerable<MovereqRecord> records)
        {
            var moveCommands = new List<MoveCommandInfo>();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.Main))
                {
                    continue;
                }

                if (record.Main.StartsWith("/move ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var displayBase = string.IsNullOrWhiteSpace(record.Sub) ? record.Main : record.Sub;
                var displayName = BuildDisplayName(displayBase);
                var normalizedIndex = NormalizeWarpIndex(record);

                moveCommands.Add(new MoveCommandInfo
                {
                    Index = normalizedIndex,
                    ServerMapName = record.Main,
                    DisplayName = displayName,
                    RequiredLevel = record.RequiredLevel,
                    RequiredZen = record.RequiredZen,
                    IsEventMap = IsEventMap(record.Index)
                });
            }

            return moveCommands;
        }

        private static void ApplyMovereqXor(byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] ^= MovereqXorKey[i % MovereqXorKey.Length];
            }
        }

        private static string ReadMovereqString(byte[] buffer, int offset, int length)
        {
            var span = new ReadOnlySpan<byte>(buffer, offset, length);
            var end = span.IndexOf((byte)0);
            if (end < 0)
            {
                end = length;
            }

            return Encoding.UTF8.GetString(buffer, offset, end);
        }

        private static string BuildDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return rawName;
            }

            var normalized = rawName.Replace('_', ' ');
            normalized = InsertSpacesBeforeDigits(normalized);
            return normalized.Trim();
        }

        private static string InsertSpacesBeforeDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 4);
            var previous = '\0';
            foreach (var character in value)
            {
                if (char.IsDigit(character) && previous != '\0' && char.IsLetter(previous))
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                previous = character;
            }

            return builder.ToString();
        }

        private static bool IsEventMap(int index) => index == 1 || index == 23;

        private static int NormalizeWarpIndex(MovereqRecord record)
        {
            if (record.Index == 42 &&
                (string.Equals(record.Main, "Vulcanus", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(record.Sub, "Vulcanus", StringComparison.OrdinalIgnoreCase)))
            {
                return 37;
            }

            return record.Index;
        }

        private static readonly List<MovereqRecord> DefaultMovereqRecords = new()
        {
            new MovereqRecord(1, "Arena", "Arena", 50, 400, 2000, 50),
            new MovereqRecord(2, "Lorencia", "Lorencia", 1, 400, 2000, 17),
            new MovereqRecord(3, "Noria", "Noria", 10, 400, 2000, 27),
            new MovereqRecord(4, "Devias", "Devias", 20, 400, 2000, 22),
            new MovereqRecord(5, "Devias2", "Devias2", 20, 400, 2500, 72),
            new MovereqRecord(6, "Devias3", "Devias3", 20, 400, 3000, 73),
            new MovereqRecord(7, "Devias4", "Devias4", 20, 400, 3500, 74),
            new MovereqRecord(8, "Dungeon", "Dungeon", 30, 400, 3000, 2),
            new MovereqRecord(9, "Dungeon2", "Dungeon2", 40, 400, 3500, 6),
            new MovereqRecord(10, "Dungeon3", "Dungeon3", 50, 400, 4000, 10),
            new MovereqRecord(11, "Atlans", "Atlans", 70, 220, 4000, 49),
            new MovereqRecord(12, "Atlans2", "Atlans2", 80, 220, 4500, 75),
            new MovereqRecord(13, "Atlans3", "Atlans3", 90, 220, 5000, 76),
            new MovereqRecord(14, "LostTower", "LostTower", 50, 400, 5000, 42),
            new MovereqRecord(15, "LostTower2", "LostTower2", 50, 400, 5500, 31),
            new MovereqRecord(16, "LostTower3", "LostTower3", 50, 400, 6000, 33),
            new MovereqRecord(17, "LostTower4", "LostTower4", 60, 400, 6500, 35),
            new MovereqRecord(18, "LostTower5", "LostTower5", 60, 400, 7000, 37),
            new MovereqRecord(19, "LostTower6", "LostTower6", 70, 400, 7500, 39),
            new MovereqRecord(20, "LostTower7", "LostTower7", 70, 400, 8000, 41),
            new MovereqRecord(21, "Tarkan", "Tarkan", 140, 400, 8000, 57),
            new MovereqRecord(22, "Tarkan2", "Tarkan2", 140, 400, 8500, 77),
            new MovereqRecord(23, "Icarus", "Icarus", 170, 400, 10000, 63),
            new MovereqRecord(25, "Aida1", "Aida1", 150, 400, 8500, 119),
            new MovereqRecord(26, "Crywolf", "Crywolf Fortress", 300, 400, 15000, 401),
            new MovereqRecord(27, "Aida2", "Aida2", 150, 400, 8500, 140),
            new MovereqRecord(28, "KanturuRuins1", "Kanturu_ruin1", 160, 400, 9000, 138),
            new MovereqRecord(29, "KanturuRuins2", "Kanturu_ruin2", 160, 400, 9000, 141),
            new MovereqRecord(30, "KanturuRelics", "Kanturu_Remain", 230, 400, 12000, 139),
            new MovereqRecord(31, "Elveland", "Elbeland", 10, 400, 2000, 267),
            new MovereqRecord(32, "Elveland2", "Elbeland2", 10, 400, 2500, 268),
            new MovereqRecord(33, "PeaceSwamp", "Swamp_of_Calmness", 400, 400, 15000, 273),
            new MovereqRecord(34, "LaCleon", "Raklion", 280, 400, 15000, 287),
            new MovereqRecord(42, "Vulcanus", "Vulcanus", 30, 400, 15000, 294),
            new MovereqRecord(43, "Elveland3", "Elbeland3", 10, 400, 3000, 269),
            new MovereqRecord(44, "LorenMarket", "LorenMarket", 200, 400, 18000, 407),
            new MovereqRecord(45, "KanturuRuins3", "Kanturu_ruin_island", 160, 400, 15000, 334),
            new MovereqRecord(46, "Karutan1", "Karutan1", 170, 400, 13000, 335),
            new MovereqRecord(47, "Karutan2", "Karutan2", 170, 400, 14000, 344),
            new MovereqRecord(48, "Raklion", "LaCleon", 280, 400, 15000, 406),
            new MovereqRecord(131, "Barracks", "Barracks of Balgass", 300, 400, 15000, 402),
            new MovereqRecord(132, "Land of Trials", "Land_of_Trials", 300, 400, 10000, 405),
            new MovereqRecord(133, "Refuge", "Balgass Refuge", 300, 400, 20000, 403),
            new MovereqRecord(134, "ForLosingZen Lorencia", "Silent Map?", 1, 400, 20000000, 408),
            new MovereqRecord(135, "LaCleon2", "LaCleon", 300, 400, 15000, 409),
            new MovereqRecord(136, "Kalima7", "Kalima 7", 300, 400, 15000, 404),
        };
    }
}
