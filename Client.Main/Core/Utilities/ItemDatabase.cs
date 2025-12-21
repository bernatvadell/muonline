// <file path="Client.Main/Core/Utilities/ItemDatabase.cs">
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Client.Data.BMD;
using Client.Main.Controls.UI.Game.Inventory;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Client.Main.Core.Utilities
{
    public static class ItemDatabase
    {
        /// <summary>Lookup cache: (Group, Id) → item definition.</summary>
        private static readonly Lazy<Dictionary<(byte Group, short Id), ItemDefinition>> _definitions;
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger("ItemDatabase");

        static ItemDatabase()
        {
            // Use Lazy with Task.Run to avoid blocking the static constructor
            // This prevents deadlocks when ItemBMDReader.Load() needs the UI thread
            _definitions = new Lazy<Dictionary<(byte Group, short Id), ItemDefinition>>(() =>
            {
                // Run initialization on a background thread to avoid deadlock
                return Task.Run(InitializeItemDataAsync).GetAwaiter().GetResult();
            });
        }

        /// <summary>
        /// Loads items.bmd from an embedded resource and builds the definition table.
        /// </summary>
        private static async Task<Dictionary<(byte, short), ItemDefinition>> InitializeItemDataAsync()
        {
            var data = new Dictionary<(byte, short), ItemDefinition>();

            var assembly = Assembly.GetExecutingAssembly();

            // Find *one* resource whose name ends with "item.bmd"
            var resourceName = assembly.GetManifestResourceNames()
                                       .SingleOrDefault(n =>
                                           n.EndsWith("item.bmd", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                Console.WriteLine(
                    "Embedded resource 'item.bmd' not found. " +
                    "Verify Build Action = Embedded Resource and correct RootNamespace.");
                return data;
            }

            try
            {
                using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    Console.WriteLine($"Failed to open resource stream '{resourceName}'.");
                    return data;
                }

                var reader = new ItemBMDReader();

                // Some ItemBMDReader implementations accept only a file path.
                // Copy the resource to a temporary file and load it from disk.
                IEnumerable<ItemBMD> items;
                var tempPath = Path.GetTempFileName();
                try
                {
                    using (var tempFs = File.OpenWrite(tempPath))
                        await resourceStream.CopyToAsync(tempFs);

                    // Load asynchronously to avoid blocking the thread
                    items = await reader.Load(tempPath).ConfigureAwait(false);
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { /* ignore IO errors */ }
                }

                foreach (var item in items)
                {
                    var key = ((byte)item.ItemSubGroup, (short)item.ItemSubIndex);
                    if (data.ContainsKey(key))
                        continue;

                    string texturePath = null;
                    if (!string.IsNullOrEmpty(item.szModelFolder) &&
                        !string.IsNullOrEmpty(item.szModelName))
                    {
                        texturePath = Path.Combine(
                                          item.szModelFolder.Replace("Data\\", string.Empty)
                                                            .Replace("Data/", string.Empty),
                                          item.szModelName)
                                          .Replace("\\", "/");
                    }

                    var itemName = item.szItemName?.Split('\t')[0].Trim() ?? string.Empty;

                    int width = item.Width;
                    int height = item.Height;

                    // Fix for bows and crossbows - they should always be 2 slots wide
                    // Exclude arrows and bolts (ammunition)
                    if (item.ItemSubGroup == 4 && itemName != "Arrow" && itemName != "Bolt") // Group 4
                    {
                        width = 2;
                    }

                    var definition = new ItemDefinition(
                        id: item.ItemSubIndex,
                        name: itemName,
                        width: width,
                        height: height,
                        texturePath: texturePath)
                    {
                        DamageMin = item.DamageMin,
                        DamageMax = item.DamageMax,
                        MagicPower = item.MagicPower,
                        AttackSpeed = item.AttackSpeed,
                        Defense = item.Defense,
                        DefenseRate = item.DefenseRate,
                        BaseDurability = item.Durability,
                        MagicDurability = item.MagicDur,
                        WalkSpeed = item.WalkSpeed,
                        DropLevel = item.DropLevel,
                        RequiredStrength = item.ReqStr,
                        RequiredDexterity = item.ReqDex,
                        RequiredEnergy = item.ReqEne,
                        RequiredLevel = item.ReqLvl,
                        TwoHanded = item.TwoHands != 0,
                        Group = item.ItemSubGroup,
                        AllowedClasses = BuildAllowedClasses(item),
                        IsExpensive = item.Expensive != 0,
                        CanSellToNpc = item.SellNpc != 0,
                        Money = item.Money,
                        ItemValue = item.ItemValue
                    };

                    data.Add(key, definition);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while loading 'items.bmd': {ex}");
            }

            return data;
        }

        #region Public API ------------------------------------------------------

        public static ItemDefinition GetItemDefinition(byte group, short id)
        {
            _definitions.Value.TryGetValue((group, id), out var def);

            return def;
        }

        public static bool TryGetItemDefinitionByName(string name, out ItemDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // Fast path: exact (case-insensitive) match.
            foreach (var def in _definitions.Value.Values)
            {
                if (def?.Name != null && def.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    definition = def;
                    return true;
                }
            }

            // Fallback: normalized match (ignores punctuation and article "the").
            string normalized = NormalizeItemName(name);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            foreach (var def in _definitions.Value.Values)
            {
                if (def?.Name == null)
                {
                    continue;
                }

                if (NormalizeItemName(def.Name) == normalized)
                {
                    definition = def;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeItemName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);
            bool prevSpace = false;

            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    prevSpace = false;
                    continue;
                }

                if (!prevSpace)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }

            var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            // Remove "the" to match strings like "Scroll of the Emperor" vs "Scroll of Emperor".
            var filtered = parts.Where(p => !p.Equals("the", StringComparison.Ordinal)).ToArray();
            return string.Join(' ', filtered);
        }

        public static IEnumerable<ItemDefinition> GetItemDefinitions(byte group)
        {
            return _definitions.Value.Where(predicate => predicate.Key.Group == group).Select(p => p.Value);
        }

        public static IEnumerable<ItemDefinition> GetWeapons()
        {
            return _definitions.Value.Where(predicate => predicate.Key.Group <= 6).Select(p => p.Value);
        }
        public static IEnumerable<ItemDefinition> GetWings()
        {
            List<short> wingIds = [
                0, 1, 2, 3, 4, 5, 6,
                27, 28,
                36, 37, 38, 39, 40, 41, 42, 43,
                49, 50,
                130, 131, 132, 133, 134, 135,
                152, 154, 155, 156, 157, 158, 159, 160,
                172, 173, 174, 175, 176, 177, 178,
                180, 181,182, 183, 184, 185, 186, 187, 188, 189,
                190, 191, 192, 193,
                262, 263, 264, 265, 266, 267, 268, 269, 270,
                278, 279, 280, 281, 282, 283, 284, 285, 286, 287,
                414, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 428, 429, 430, 431, 432, 433, 434, 435, 436, 437,
                467, 468, 469, 472, 473, 474,
                480, 489, 490, 496,
            ];
            return _definitions.Value.Where(predicate => predicate.Key.Group == 12 && wingIds.Contains(predicate.Key.Id)).Select(p => p.Value);
        }
        public static IEnumerable<ItemDefinition> GetArmors()
        {
            return _definitions.Value.Where(predicate => predicate.Key.Group == 8).Select(p => p.Value);
        }
        public static IEnumerable<ItemDefinition> GetPets()
        {
            return _definitions.Value.Where(predicate => predicate.Key.Group == 13).Select(p => p.Value);
        }

        public static ItemDefinition GetItemDefinition(ReadOnlySpan<byte> itemData)
        {
            if (itemData.Length < 6) return null;
            short id = itemData[0];
            byte group = (byte)(itemData[5] >> 4);
            return GetItemDefinition(group, id);
        }

        public static byte GetItemGroup(ReadOnlySpan<byte> itemData)
        {
            if (itemData.Length < 6) return 0;
            byte group = (byte)(itemData[5] >> 4);
            return group;
        }

        public static string GetItemName(byte group, short id) =>
            GetItemDefinition(group, id)?.Name;

        public static string GetItemName(ReadOnlySpan<byte> itemData) =>
            GetItemDefinition(itemData)?.Name;

        public struct ItemDetails
        {
            public int Level;
            public bool HasSkill;
            public bool HasLuck;
            public int OptionLevel;
            public bool IsExcellent;
            public bool IsAncient;

            public bool HasBlueOptions => HasSkill || HasLuck || OptionLevel > 0;
        }

        public static ItemDetails ParseItemDetails(ReadOnlySpan<byte> itemData)
        {
            var d = new ItemDetails();
            if (itemData.IsEmpty || itemData.Length < 3) return d;

            byte optByte = itemData[1];
            byte excByte = itemData.Length > 3 ? itemData[3] : (byte)0;
            byte ancientByte = itemData.Length > 4 ? itemData[4] : (byte)0;

            d.Level = (optByte & 0x78) >> 3;
            d.HasSkill = (optByte & 0x80) != 0;
            d.HasLuck = (optByte & 0x04) != 0;

            int optionLevel = optByte & 0x03;
            if ((excByte & 0x40) != 0) optionLevel |= 0b100;
            d.OptionLevel = optionLevel;

            d.IsExcellent = (excByte & 0x3F) != 0;
            d.IsAncient = (ancientByte & 0x0F) > 0;

            return d;
        }

        public static List<string> ParseExcellentOptions(byte excByte)
        {
            var list = new List<string>();
            if ((excByte & 0b0000_0001) != 0) list.Add("MP/8");
            if ((excByte & 0b0000_0010) != 0) list.Add("HP/8");
            if ((excByte & 0b0000_0100) != 0) list.Add("Dmg%");
            if ((excByte & 0b0000_1000) != 0) list.Add("Speed");
            if ((excByte & 0b0001_0000) != 0) list.Add("Rate");
            if ((excByte & 0b0010_0000) != 0) list.Add("Zen");
            return list;
        }


        public static string ParseSocketOption(byte socketByte) => $"S:{socketByte}";

        #endregion

        #region Helpers ---------------------------------------------------------

        private static List<string> BuildAllowedClasses(ItemBMD item)
        {
            var classes = new List<string>();
            if (item.DW != 0) classes.Add("Dark Wizard");
            if (item.DK != 0) classes.Add("Dark Knight");
            if (item.FE != 0) classes.Add("Fairy Elf");
            if (item.MG != 0) classes.Add("Magic Gladiator");
            if (item.DL != 0) classes.Add("Dark Lord");
            if (item.SU != 0) classes.Add("Summoner");
            if (item.RF != 0) classes.Add("Rage Fighter");
            if (item.GL != 0) classes.Add("Grow Lancer");
            if (item.RW != 0) classes.Add("Rune Wizard");
            if (item.SL != 0) classes.Add("Slayer");
            if (item.GC != 0) classes.Add("Gun Crusher");
            return classes;
        }

        #endregion
    }
}
