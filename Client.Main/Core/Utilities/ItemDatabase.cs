// <file path="Client.Main/Core/Utilities/ItemDatabase.cs">
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Client.Main.Controls.UI.Game.Inventory;

namespace Client.Main.Core.Utilities
{
    public static class ItemDatabase
    {
        private class JsonItem
        {
            [JsonPropertyName("ItemSubGroup")] public byte ItemSubGroup { get; set; }
            [JsonPropertyName("ItemSubIndex")] public short ItemSubIndex { get; set; }
            [JsonPropertyName("szModelFolder")] public string SzModelFolder { get; set; }
            [JsonPropertyName("szModelName")] public string SzModelName { get; set; }
            [JsonPropertyName("szItemName")] public object SzItemName { get; set; }
            [JsonPropertyName("Width")] public int Width { get; set; }
            [JsonPropertyName("Height")] public int Height { get; set; }
        }

        private static readonly Dictionary<(byte Group, short Id), ItemDefinition> _definitions;

        static ItemDatabase()
        {
            _definitions = InitializeItemData();
        }

        private static Dictionary<(byte, short), ItemDefinition> InitializeItemData()
        {
            var data = new Dictionary<(byte, short), ItemDefinition>();
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Client.Main.Data.items.json";

            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Console.WriteLine($"Error: Embedded resource '{resourceName}' not found.");
                        return data;
                    }

                    using (JsonDocument doc = JsonDocument.Parse(stream))
                    {
                        if (doc.RootElement.ValueKind != JsonValueKind.Array)
                        {
                            Console.WriteLine("Error: items.json root is not an array.");
                            return data;
                        }

                        foreach (JsonElement element in doc.RootElement.EnumerateArray())
                        {
                            try
                            {
                                var jsonItem = element.Deserialize<JsonItem>();
                                if (jsonItem == null) continue;

                                var key = (jsonItem.ItemSubGroup, jsonItem.ItemSubIndex);
                                if (!data.ContainsKey(key))
                                {
                                    string texturePath = null;
                                    if (!string.IsNullOrEmpty(jsonItem.SzModelFolder) && !string.IsNullOrEmpty(jsonItem.SzModelName))
                                    {
                                        texturePath = Path.Combine(
                                            jsonItem.SzModelFolder.Replace("Data\\", "").Replace("Data/", ""),
                                            jsonItem.SzModelName
                                        ).Replace("\\", "/");
                                    }

                                    string itemName = jsonItem.SzItemName?.ToString() ?? string.Empty;
                                    if (itemName.Contains('\t'))
                                    {
                                        itemName = itemName.Split('\t')[0].Trim();
                                    }

                                    var definition = new ItemDefinition(
                                        id: jsonItem.ItemSubIndex,
                                        name: itemName,
                                        width: jsonItem.Width,
                                        height: jsonItem.Height,
                                        texturePath: texturePath
                                    );
                                    data.Add(key, definition);
                                }
                            }
                            catch (JsonException jsonEx)
                            {
                                string rawJson = element.ToString();
                                Console.WriteLine($"Skipping malformed item entry in items.json: {rawJson.Substring(0, Math.Min(120, rawJson.Length))}... Error: {jsonEx.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load or parse items.json: {ex}");
            }

            return data;
        }

        public static ItemDefinition GetItemDefinition(byte group, short id)
        {
            _definitions.TryGetValue((group, id), out var definition);
            return definition;
        }

        public static ItemDefinition GetItemDefinition(ReadOnlySpan<byte> itemData)
        {
            if (itemData.Length < 6) return null;
            short id = itemData[0];
            byte group = (byte)(itemData[5] >> 4);
            return GetItemDefinition(group, id);
        }

        public static string GetItemName(byte group, short id)
        {
            return GetItemDefinition(group, id)?.Name;
        }

        public static string GetItemName(ReadOnlySpan<byte> itemData)
        {
            return GetItemDefinition(itemData)?.Name;
        }

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
            var details = new ItemDetails();
            if (itemData.IsEmpty || itemData.Length < 3) return details;
            byte optionLevelByte = itemData[1];
            byte excByte = itemData.Length > 3 ? itemData[3] : (byte)0;
            byte ancientByte = itemData.Length > 4 ? itemData[4] : (byte)0;
            details.Level = (optionLevelByte & 0x78) >> 3;
            details.HasSkill = (optionLevelByte & 0x80) != 0;
            details.HasLuck = (optionLevelByte & 0x04) != 0;
            int optionLevel = (optionLevelByte & 0x03);
            if ((excByte & 0x40) != 0) optionLevel |= 0b100;
            details.OptionLevel = optionLevel;
            details.IsExcellent = (excByte & 0x3F) != 0;
            details.IsAncient = (ancientByte & 0x0F) > 0;
            return details;
        }

        public static List<string> ParseExcellentOptions(byte excByte)
        {
            var options = new List<string>();
            if ((excByte & 0b0000_0001) != 0) options.Add("MP/8");
            if ((excByte & 0b0000_0010) != 0) options.Add("HP/8");
            if ((excByte & 0b0000_1000) != 0) options.Add("Speed");
            if ((excByte & 0b0000_0100) != 0) options.Add("Dmg%");
            if ((excByte & 0b0001_0000) != 0) options.Add("Rate");
            if ((excByte & 0b0010_0000) != 0) options.Add("Zen");
            return options;
        }

        public static string ParseSocketOption(byte socketByte)
        {
            return $"S:{socketByte}";
        }
    }
}