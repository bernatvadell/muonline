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

namespace Client.Main.Core.Utilities
{
    public static class ItemDatabase
    {
        /// <summary>Lookup cache: (Group, Id) → item definition.</summary>
        private static readonly Dictionary<(byte Group, short Id), ItemDefinition> _definitions;
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger("ItemDatabase");

        static ItemDatabase() => _definitions = InitializeItemData();

        /// <summary>
        /// Loads items.bmd from an embedded resource and builds the definition table.
        /// </summary>
        private static Dictionary<(byte, short), ItemDefinition> InitializeItemData()
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
                        resourceStream.CopyTo(tempFs);

                    items = reader.Load(tempPath)
                                  .ConfigureAwait(false)
                                  .GetAwaiter()
                                  .GetResult();
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

                    var definition = new ItemDefinition(
                        id: item.ItemSubIndex,
                        name: itemName,
                        width: item.Width,
                        height: item.Height,
                        texturePath: texturePath)
                    {
                        DamageMin = item.DamageMin,
                        DamageMax = item.DamageMax,
                        AttackSpeed = item.AttackSpeed,
                        Defense = item.Defense,
                        DefenseRate = item.DefenseRate,
                        BaseDurability = item.Durability,
                        RequiredStrength = item.ReqStr,
                        RequiredDexterity = item.ReqDex,
                        RequiredEnergy = item.ReqEne,
                        RequiredLevel = item.ReqLvl,
                        TwoHanded = item.TwoHands != 0,
                        Group = item.ItemSubGroup,
                        AllowedClasses = BuildAllowedClasses(item)
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
            _definitions.TryGetValue((group, id), out var def);
            
            return def;
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
