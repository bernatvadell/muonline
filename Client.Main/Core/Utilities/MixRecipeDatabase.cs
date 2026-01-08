using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Data;
using Client.Main;
using Client.Main.Controls.UI.Game.Inventory;
using Microsoft.Extensions.Logging;

namespace Client.Main.Core.Utilities
{
    public enum MixInventoryType
    {
        GoblinNormal = 0,
        GoblinChaosItem = 1,
        GoblinAdd380 = 2,
        CastleSenior = 3,
        Trainer = 4,
        Osbourne = 5,
        Jerridon = 6,
        Elpis = 7,
        ChaosCard = 8,
        CherryBlossom = 9,
        ExtractSeed = 10,
        SeedSphere = 11,
        AttachSocket = 12,
        DetachSocket = 13,
    }

    public enum MixRateOp
    {
        Number = 0,
        Add = 1,
        Sub = 2,
        Mul = 3,
        Div = 4,
        Lp = 5,
        Rp = 6,
        Int = 7,

        MaxRate = 32,
        Item = 33,
        Wing = 34,
        Excellent = 35,
        Equip = 36,
        Set = 37,
        Level1 = 38,
        NonJewelItem = 39,

        LuckOpt = 64
    }

    [Flags]
    public enum MixSpecialItem
    {
        None = 0,
        Excellent = 1,
        Add380Item = 2,
        SetItem = 4,
        Harmony = 8,
        SocketItem = 16,
    }

    public readonly record struct MixRateToken(MixRateOp Op, float Value);

    public readonly record struct MixRecipeItem(
        short TypeMin,
        short TypeMax,
        int LevelMin,
        int LevelMax,
        int OptionMin,
        int OptionMax,
        int DurabilityMin,
        int DurabilityMax,
        int CountMin,
        int CountMax,
        MixSpecialItem SpecialItem);

    public sealed record MixRecipe
    {
        public int MixIndex { get; init; }
        public int MixId { get; init; }
        public int[] NameTextKeys { get; init; } = new int[3];
        public int[] DescTextKeys { get; init; } = new int[3];
        public int[] AdviceTextKeys { get; init; } = new int[3];
        public int Width { get; init; }
        public int Height { get; init; }
        public int RequiredLevel { get; init; }
        public char RequiredZenType { get; init; }
        public uint RequiredZen { get; init; }
        public int NumRateData { get; init; }
        public MixRateToken[] RateTokens { get; init; } = new MixRateToken[32];
        public int MaxSuccessRate { get; init; }
        public char MixOption { get; init; }
        public char CharmOption { get; init; }
        public char ChaosCharmOption { get; init; }
        public MixRecipeItem[] Sources { get; init; } = new MixRecipeItem[8];
        public int NumSources { get; init; }
    }

    public sealed class ChaosMixEvaluation
    {
        public MixRecipe CurrentRecipe { get; init; }
        public MixRecipe MostSimilarRecipe { get; init; }
        public int SuccessRate { get; init; }
        public uint RequiredZen { get; init; }
    }

    public static class MixRecipeDatabase
    {
        private const int MaxMixTypes = 14;
        private const int RecipeSizeBytes = 656;

        private static readonly ILogger s_logger = MuGame.AppLoggerFactory?.CreateLogger("MixRecipeDatabase");

        private sealed class MixDatabaseState
        {
            public IReadOnlyList<MixRecipe>[] RecipesByType { get; init; }
        }

        private static readonly Lazy<MixDatabaseState> s_state = new(LoadInternal, isThreadSafe: true);

        public static IReadOnlyList<MixRecipe> GetRecipes(MixInventoryType type)
        {
            var list = s_state.Value.RecipesByType[(int)type];
            return list ?? Array.Empty<MixRecipe>();
        }

        public static IReadOnlyList<MixRecipe> GetChaosMachineRecipes()
        {
            // Chaos Goblin uses the first three mix groups.
            return GetRecipes(MixInventoryType.GoblinNormal)
                .Concat(GetRecipes(MixInventoryType.GoblinChaosItem))
                .Concat(GetRecipes(MixInventoryType.GoblinAdd380))
                .ToList();
        }

        public static ChaosMixEvaluation EvaluateChaosMachine(IReadOnlyList<InventoryItem> items)
        {
            var recipes = GetChaosMachineRecipes();
            if (recipes.Count == 0)
            {
                return new ChaosMixEvaluation();
            }

            var mixItems = BuildMixItems(items);

            // 1) Exact match
            foreach (var recipe in recipes)
            {
                ResetTestCounts(mixItems);
                if (CheckRecipeSub(recipe, mixItems, out var evalState))
                {
                    var successRate = CalcMixRate(recipe, mixItems, evalState);
                    var requiredZen = CalcRequiredZen(recipe, successRate);
                    return new ChaosMixEvaluation
                    {
                        CurrentRecipe = recipe,
                        MostSimilarRecipe = recipe,
                        SuccessRate = successRate,
                        RequiredZen = requiredZen
                    };
                }
            }

            // 2) Similar recipe (best effort)
            ResetTestCounts(mixItems);
            var similar = FindMostSimilarRecipe(recipes, mixItems);
            return new ChaosMixEvaluation
            {
                CurrentRecipe = null,
                MostSimilarRecipe = similar,
                SuccessRate = 0,
                RequiredZen = 0
            };
        }

        // ─────────────────────────── Parsing ───────────────────────────

        private static MixDatabaseState LoadInternal()
        {
            var recipesByType = new IReadOnlyList<MixRecipe>[MaxMixTypes];
            for (int i = 0; i < recipesByType.Length; i++)
            {
                recipesByType[i] = Array.Empty<MixRecipe>();
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .SingleOrDefault(n => n.EndsWith("mix.bmd", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                s_logger?.LogWarning("Embedded resource 'mix.bmd' not found. Add it to Client.Main.Shared.props as EmbeddedResource.");
                return new MixDatabaseState { RecipesByType = recipesByType };
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                s_logger?.LogWarning("Failed to open embedded resource stream '{ResourceName}'.", resourceName);
                return new MixDatabaseState { RecipesByType = recipesByType };
            }

            try
            {
                using var br = new BinaryReader(stream);

                byte[] headerEnc = br.ReadBytes(sizeof(int) * MaxMixTypes);
                if (headerEnc.Length != sizeof(int) * MaxMixTypes)
                {
                    s_logger?.LogWarning("mix.bmd header too short: {Len}", headerEnc.Length);
                    return new MixDatabaseState { RecipesByType = recipesByType };
                }

                byte[] header = BuxCryptor.Convert(headerEnc);
                int[] counts = new int[MaxMixTypes];
                for (int i = 0; i < counts.Length; i++)
                {
                    counts[i] = BitConverter.ToInt32(header, i * 4);
                }

                long expectedSize = (sizeof(int) * MaxMixTypes) + (counts.Sum(c => Math.Max(0, c)) * RecipeSizeBytes);
                if (stream.CanSeek && stream.Length != expectedSize)
                {
                    s_logger?.LogWarning("mix.bmd size mismatch. Expected {Expected} bytes, actual {Actual} bytes.", expectedSize, stream.Length);
                }

                for (int mixType = 0; mixType < MaxMixTypes; mixType++)
                {
                    int count = counts[mixType];
                    if (count <= 0)
                    {
                        recipesByType[mixType] = Array.Empty<MixRecipe>();
                        continue;
                    }

                    var list = new List<MixRecipe>(count);
                    for (int i = 0; i < count; i++)
                    {
                        byte[] recEnc = br.ReadBytes(RecipeSizeBytes);
                        if (recEnc.Length != RecipeSizeBytes)
                        {
                            s_logger?.LogWarning("mix.bmd truncated while reading recipe. mixType={MixType}, idx={Idx}, got={Len}.", mixType, i, recEnc.Length);
                            break;
                        }

                        byte[] rec = BuxCryptor.Convert(recEnc);
                        list.Add(ParseRecipe(rec));
                    }

                    recipesByType[mixType] = list;
                }
            }
            catch (Exception ex)
            {
                s_logger?.LogError(ex, "Error while loading mix.bmd.");
                return new MixDatabaseState { RecipesByType = recipesByType };
            }

            s_logger?.LogInformation("Loaded mix.bmd recipes: {Counts}", string.Join(",", recipesByType.Select(l => l?.Count ?? 0)));
            return new MixDatabaseState { RecipesByType = recipesByType };
        }

        private static MixRecipe ParseRecipe(byte[] data)
        {
            using var ms = new MemoryStream(data, writable: false);
            using var br = new BinaryReader(ms);

            var recipe = new MixRecipe
            {
                MixIndex = br.ReadInt32(),
                MixId = br.ReadInt32()
            };

            for (int i = 0; i < 3; i++) recipe.NameTextKeys[i] = br.ReadInt32();
            for (int i = 0; i < 3; i++) recipe.DescTextKeys[i] = br.ReadInt32();
            for (int i = 0; i < 3; i++) recipe.AdviceTextKeys[i] = br.ReadInt32();

            recipe = recipe with
            {
                Width = br.ReadInt32(),
                Height = br.ReadInt32(),
                RequiredLevel = br.ReadInt32(),
                RequiredZenType = (char)br.ReadByte()
            };

            br.ReadBytes(3); // padding

            recipe = recipe with
            {
                RequiredZen = br.ReadUInt32(),
                NumRateData = br.ReadInt32()
            };

            var tokens = new MixRateToken[32];
            for (int i = 0; i < 32; i++)
            {
                var op = (MixRateOp)br.ReadInt32();
                float value = br.ReadSingle();
                tokens[i] = new MixRateToken(op, value);
            }

            recipe = recipe with { RateTokens = tokens };

            int maxSuccessRate = br.ReadInt32();
            byte mixOption = br.ReadByte();
            byte charmOption = br.ReadByte();
            byte chaosCharmOption = br.ReadByte();
            br.ReadByte(); // padding

            var sources = new MixRecipeItem[8];
            for (int i = 0; i < sources.Length; i++)
            {
                short typeMin = br.ReadInt16();
                short typeMax = br.ReadInt16();
                int levelMin = br.ReadInt32();
                int levelMax = br.ReadInt32();
                int optMin = br.ReadInt32();
                int optMax = br.ReadInt32();
                int duraMin = br.ReadInt32();
                int duraMax = br.ReadInt32();
                int countMin = br.ReadInt32();
                int countMax = br.ReadInt32();
                var special = (MixSpecialItem)br.ReadUInt32();

                sources[i] = new MixRecipeItem(typeMin, typeMax, levelMin, levelMax, optMin, optMax, duraMin, duraMax, countMin, countMax, special);
            }

            int numSources = br.ReadInt32();

            return recipe with
            {
                MaxSuccessRate = maxSuccessRate,
                MixOption = (char)mixOption,
                CharmOption = (char)charmOption,
                ChaosCharmOption = (char)chaosCharmOption,
                Sources = sources,
                NumSources = numSources
            };
        }

        // ──────────────────────── Evaluation ─────────────────────────

        private sealed class MixEvalState
        {
            public bool FindLuck;
            public uint TotalItemValue;
            public uint ExcellentItemValue;
            public uint EquipmentItemValue;
            public uint WingItemValue;
            public uint SetItemValue;
            public uint TotalNonJewelItemValue;
            public int FirstItemLevel;
        }

        private sealed class MixItem
        {
            public short Type;
            public int Level;
            public int Option;
            public int Durability;
            public int Count;
            public int TestCount;
            public MixSpecialItem SpecialItem;
            public bool IsCharmItem;
            public bool IsChaosCharmItem;
            public bool IsJewelItem;
            public bool MixLuck;
            public bool IsEquipment;
            public bool IsWing;
            public uint MixValue;
            public InventoryItem SourceItem;
        }

        private static readonly Lazy<HashSet<short>> s_wingTypes = new(() =>
        {
            try
            {
                return ItemDatabase.GetWings()
                    .Select(d => (short)((d.Group * 512) + d.Id))
                    .ToHashSet();
            }
            catch
            {
                return new HashSet<short>();
            }
        });

        private static List<MixItem> BuildMixItems(IReadOnlyList<InventoryItem> items)
        {
            var list = new List<MixItem>(items?.Count ?? 0);
            if (items == null)
            {
                return list;
            }

            foreach (var it in items)
            {
                if (it?.Definition == null)
                {
                    continue;
                }

                short type = (short)((it.Definition.Group * 512) + it.Definition.Id);
                bool canStack = it.Definition.BaseDurability == 0 && it.Definition.MagicDurability == 0;
                int count = canStack ? Math.Max(0, it.Durability) : 1;

                var special = MixSpecialItem.None;
                if (it.Details.IsExcellent) special |= MixSpecialItem.Excellent;
                if (it.Details.IsAncient) special |= MixSpecialItem.SetItem;
                if (it.Definition.RequiredLevel >= 380) special |= MixSpecialItem.Add380Item;

                bool isJewel = it.Definition.IsJewel();
                bool isEquipment = it.Definition.Group <= 11;
                bool isWing = s_wingTypes.Value.Contains(type);
                bool isCharm = it.Definition.Group == 14 && it.Definition.Id == 53;
                bool isChaosCharm = it.Definition.Group == 14 && it.Definition.Id == 96;

                list.Add(new MixItem
                {
                    SourceItem = it,
                    Type = type,
                    Level = it.Details.Level,
                    Option = it.Details.OptionLevel * 4,
                    Durability = it.Durability,
                    Count = count,
                    TestCount = count,
                    SpecialItem = special,
                    IsJewelItem = isJewel,
                    IsEquipment = isEquipment,
                    IsWing = isWing,
                    IsCharmItem = isCharm,
                    IsChaosCharmItem = isChaosCharm,
                    MixLuck = it.Details.HasLuck,
                    MixValue = EvaluateMixItemValue(type, it.Definition)
                });
            }

            return list;
        }

        private static void ResetTestCounts(List<MixItem> items)
        {
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].TestCount = items[i].Count;
            }
        }

        private static bool IsOptionItem(in MixRecipeItem item) => item.CountMin == 0;

        private static bool CheckItem(in MixRecipeItem recipeItem, MixItem source)
        {
            if (recipeItem.TypeMin <= source.Type && recipeItem.TypeMax >= source.Type &&
                recipeItem.LevelMin <= source.Level && recipeItem.LevelMax >= source.Level &&
                recipeItem.DurabilityMin <= source.Durability && recipeItem.DurabilityMax >= source.Durability &&
                recipeItem.OptionMin <= source.Option && recipeItem.OptionMax >= source.Option &&
                (recipeItem.SpecialItem & MixSpecialItem.Excellent) <= (source.SpecialItem & MixSpecialItem.Excellent) &&
                (recipeItem.SpecialItem & MixSpecialItem.Add380Item) <= (source.SpecialItem & MixSpecialItem.Add380Item) &&
                (recipeItem.SpecialItem & MixSpecialItem.SetItem) <= (source.SpecialItem & MixSpecialItem.SetItem) &&
                (recipeItem.SpecialItem & MixSpecialItem.Harmony) <= (source.SpecialItem & MixSpecialItem.Harmony) &&
                (recipeItem.SpecialItem & MixSpecialItem.SocketItem) <= (source.SpecialItem & MixSpecialItem.SocketItem))
            {
                return true;
            }
            return false;
        }

        private static bool CheckRecipeSub(MixRecipe recipe, List<MixItem> mixItems, out MixEvalState state)
        {
            state = new MixEvalState();
            if (recipe == null) return false;

            bool bFind = false;
            int[] recipeTest = new int[8];

            for (int j = 0; j < recipe.NumSources && j < recipe.Sources.Length; j++)
            {
                var sourceReq = recipe.Sources[j];
                if (!IsOptionItem(sourceReq))
                {
                    bFind = false;
                }

                for (int i = 0; i < mixItems.Count; i++)
                {
                    var src = mixItems[i];
                    if (CheckItem(sourceReq, src) && src.TestCount > 0 &&
                        sourceReq.CountMax >= recipeTest[j] + src.TestCount)
                    {
                        if (src.TestCount >= sourceReq.CountMax)
                        {
                            recipeTest[j] += sourceReq.CountMax;
                            src.TestCount -= sourceReq.CountMax;
                        }
                        else
                        {
                            recipeTest[j] += src.TestCount;
                            src.TestCount = 0;
                        }

                        bFind = true;

                        if (j == 0)
                        {
                            state.FirstItemLevel = src.Level;
                        }
                    }
                }

                if (!bFind || sourceReq.CountMin > recipeTest[j])
                {
                    return false;
                }
            }

            for (int i = 0; i < mixItems.Count; i++)
            {
                if (mixItems[i].TestCount <= 0) continue;

                if (mixItems[i].IsCharmItem && recipe.CharmOption == 'A')
                {
                    continue;
                }

                if (mixItems[i].IsChaosCharmItem && recipe.ChaosCharmOption == 'A')
                {
                    continue;
                }

                return false;
            }

            EvaluateMixItems(state, mixItems);
            return true;
        }

        private static void EvaluateMixItems(MixEvalState state, List<MixItem> mixItems)
        {
            state.FindLuck = false;
            state.TotalItemValue = 0;
            state.ExcellentItemValue = 0;
            state.EquipmentItemValue = 0;
            state.WingItemValue = 0;
            state.SetItemValue = 0;
            state.TotalNonJewelItemValue = 0;

            for (int i = 0; i < mixItems.Count; i++)
            {
                var it = mixItems[i];
                if (it.MixLuck) state.FindLuck = true;
                if ((it.SpecialItem & MixSpecialItem.Excellent) != 0) state.ExcellentItemValue += it.MixValue;
                if (it.IsEquipment) state.EquipmentItemValue += it.MixValue;
                if (it.IsWing) state.WingItemValue += it.MixValue;
                if ((it.SpecialItem & MixSpecialItem.SetItem) != 0) state.SetItemValue += it.MixValue;
                if (!it.IsJewelItem) state.TotalNonJewelItemValue += it.MixValue;
                state.TotalItemValue += it.MixValue;
            }
        }

        private static int CalcMixRate(MixRecipe recipe, List<MixItem> mixItems, MixEvalState state)
        {
            // Charm bonus (only type 'A' in original)
            int totalCharmBonus = 0;
            for (int i = 0; i < mixItems.Count; i++)
            {
                if (mixItems[i].IsCharmItem)
                {
                    totalCharmBonus += mixItems[i].Count;
                }
            }

            int iter = 0;
            float value = MixrateAddSub(recipe, state, ref iter);
            int successRate = (int)value;

            if (successRate > recipe.MaxSuccessRate)
            {
                successRate = recipe.MaxSuccessRate;
            }

            if (recipe.CharmOption == 'A')
            {
                successRate += totalCharmBonus;
            }

            if (successRate > 100)
            {
                successRate = 100;
            }

            if (successRate < 0)
            {
                successRate = 0;
            }

            return successRate;
        }

        private static float MixrateAddSub(MixRecipe recipe, MixEvalState state, ref int iter)
        {
            float left = 0;
            while (true)
            {
                if (iter >= recipe.NumRateData || recipe.RateTokens[iter].Op == MixRateOp.Rp)
                {
                    return left;
                }

                switch (recipe.RateTokens[iter].Op)
                {
                    case MixRateOp.Add:
                        iter++;
                        left += MixrateMulDiv(recipe, state, ref iter);
                        break;
                    case MixRateOp.Sub:
                        iter++;
                        left -= MixrateMulDiv(recipe, state, ref iter);
                        break;
                    default:
                        left = MixrateMulDiv(recipe, state, ref iter);
                        break;
                }
            }
        }

        private static float MixrateMulDiv(MixRecipe recipe, MixEvalState state, ref int iter)
        {
            float left = 0;
            while (true)
            {
                if (iter >= recipe.NumRateData || recipe.RateTokens[iter].Op == MixRateOp.Rp)
                {
                    return left;
                }

                switch (recipe.RateTokens[iter].Op)
                {
                    case MixRateOp.Add:
                    case MixRateOp.Sub:
                        return left;
                    case MixRateOp.Mul:
                        iter++;
                        left *= MixrateFactor(recipe, state, ref iter);
                        break;
                    case MixRateOp.Div:
                        iter++;
                        left /= MixrateFactor(recipe, state, ref iter);
                        break;
                    default:
                        left = MixrateFactor(recipe, state, ref iter);
                        break;
                }
            }
        }

        private static float MixrateFactor(MixRecipe recipe, MixEvalState state, ref int iter)
        {
            float value;
            var token = recipe.RateTokens[iter];
            switch (token.Op)
            {
                case MixRateOp.Lp:
                    iter++;
                    value = MixrateAddSub(recipe, state, ref iter);
                    break;
                case MixRateOp.Int:
                    iter++;
                    if (recipe.RateTokens[iter].Op != MixRateOp.Lp)
                    {
                        // Best effort: still evaluate.
                    }
                    iter++;
                    value = (int)MixrateAddSub(recipe, state, ref iter);
                    break;
                case MixRateOp.Number:
                    value = token.Value;
                    break;
                case MixRateOp.MaxRate:
                    value = recipe.MaxSuccessRate;
                    break;
                case MixRateOp.Item:
                    value = state.TotalItemValue;
                    break;
                case MixRateOp.Wing:
                    value = state.WingItemValue;
                    break;
                case MixRateOp.Excellent:
                    value = state.ExcellentItemValue;
                    break;
                case MixRateOp.Equip:
                    value = state.EquipmentItemValue;
                    break;
                case MixRateOp.Set:
                    value = state.SetItemValue;
                    break;
                case MixRateOp.LuckOpt:
                    value = state.FindLuck ? 25 : 0;
                    break;
                case MixRateOp.Level1:
                    value = state.FirstItemLevel;
                    break;
                case MixRateOp.NonJewelItem:
                    value = state.TotalNonJewelItemValue;
                    break;
                default:
                    value = 0;
                    break;
            }
            iter++;
            return value;
        }

        private static uint CalcRequiredZen(MixRecipe recipe, int successRate)
        {
            return recipe.RequiredZenType switch
            {
                'A' => recipe.RequiredZen,
                'C' => recipe.RequiredZen,
                'B' => (uint)(successRate * recipe.RequiredZen),
                _ => recipe.RequiredZen
            };
        }

        private static MixRecipe FindMostSimilarRecipe(IReadOnlyList<MixRecipe> recipes, List<MixItem> mixItems)
        {
            // Simplified similarity: first recipe which shares at least one required source with current items,
            // otherwise the first recipe.
            if (recipes == null || recipes.Count == 0)
            {
                return null;
            }

            foreach (var recipe in recipes)
            {
                for (int j = 0; j < recipe.NumSources && j < recipe.Sources.Length; j++)
                {
                    var req = recipe.Sources[j];
                    for (int i = 0; i < mixItems.Count; i++)
                    {
                        if (CheckItem(req, mixItems[i]))
                        {
                            return recipe;
                        }
                    }
                }
            }

            return recipes[0];
        }

        private static uint EvaluateMixItemValue(short type, ItemDefinition definition)
        {
            // Based on SourceMain5.2/source/MixMgr.cpp::EvaluateMixItemValue
            // Types are (Group * 512) + Id.
            // Jewel of Chaos: group 12 id 15 => 12*512+15 = 6159
            // Jewel of Bless: group 14 id 13 => 14*512+13 = 7181
            // Jewel of Soul:  group 14 id 14 => 14*512+14 = 7182
            // Jewel of Creation: group 14 id 22 => 14*512+22 = 7190
            // Jewel of Life: group 14 id 16 => 14*512+16 = 7184

            return type switch
            {
                6159 => 40000,
                7181 => 100000,
                7182 => 70000,
                7190 => 450000,
                7184 => 0,
                _ => (uint)Math.Max(0, definition?.ItemValue ?? 0)
            };
        }
    }
}
