using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game.Quest;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Networking.Services;
using Client.Main.Scenes;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles legacy quest packets (A1/A2/A3/A4) which include classic class-change quests (Sebina/Marlon/Devin).
    /// </summary>
    public class LegacyQuestHandler : IGamePacketHandler
    {
        private readonly ILogger<LegacyQuestHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly CharacterService _characterService;

        public LegacyQuestHandler(
            ILoggerFactory loggerFactory,
            CharacterState characterState,
            CharacterService characterService)
        {
            _logger = loggerFactory.CreateLogger<LegacyQuestHandler>();
            _characterState = characterState;
            _characterService = characterService;
        }

        [PacketHandler(0xA1, PacketRouter.NoSubCode)] // LegacyQuestStateDialog
        public Task HandleLegacyQuestStateDialogAsync(Memory<byte> packet)
        {
            try
            {
                var dialog = new LegacyQuestStateDialog(packet);
                byte questIndex = dialog.QuestIndex;

                int group = questIndex / 4;
                if (group is >= 0 and < 2)
                {
                    _characterState.UpdateLegacyQuestStateGroup(questIndex, dialog.State);
                }

                var state = _characterState.GetLegacyQuestState(questIndex);
                bool canProceed = state != LegacyQuestState.Complete && state != LegacyQuestState.Undefined;
                bool meetsRequirements = CheckAllRequirements(questIndex, _characterState, out var blockReason);

                var questData = BuildQuestData(questIndex, state, _characterState, blockReason);

                MuGame.ScheduleOnMainThread(() =>
                {
                    bool shouldSendCloseNpc = true;
                    var questDialog = QuestDialogControl.Instance;

                    // Always show two buttons for legacy quests (Continue/Close)
                    // Only show single OK for completed quests
                    bool isCompleted = state == LegacyQuestState.Complete;

                    _logger.LogInformation("Quest dialog: questIndex={QuestIndex}, state={State}, isCompleted={IsCompleted}",
                        questIndex, state, isCompleted);

                    questDialog.ShowQuest(
                        questData,
                        canProceed: !isCompleted, // Two buttons unless completed
                        onAccept: () =>
                        {
                            _logger.LogInformation("onAccept callback invoked! isCompleted={IsCompleted}", isCompleted);

                            try
                            {
                                if (!isCompleted)
                                {
                                    // SourceMain behavior: don't send proceed when requirements are missing.
                                    // This prevents the NPC dialog from getting stuck "open" on the server.
                                    if (!CheckAllRequirements(questIndex, _characterState, out var requirementError))
                                    {
                                        shouldSendCloseNpc = true;
                                        if (!string.IsNullOrWhiteSpace(requirementError))
                                        {
                                            MuGame.ScheduleOnMainThread(() => RequestDialog.ShowInfo(requirementError));
                                        }
                                        return;
                                    }

                                    // Requirements met -> proceed (keep NPC dialog open until server responds).
                                    shouldSendCloseNpc = false;
                                    _logger.LogInformation("Sending LegacyQuestProceedRequest for questIndex={QuestIndex}", questIndex);
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await _characterService.SendLegacyQuestProceedRequestAsync(questIndex);
                                            _logger.LogInformation("LegacyQuestProceedRequest sent successfully");
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Error in SendLegacyQuestProceedRequestAsync");
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in onAccept callback");
                            }
                        },
                        onReject: null);

                    AttachNpcCloseOnQuestDialogClosed(questDialog, () => shouldSendCloseNpc);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LegacyQuestStateDialog packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xA2, PacketRouter.NoSubCode)] // LegacySetQuestStateResponse
        public Task HandleLegacySetQuestStateResponseAsync(Memory<byte> packet)
        {
            try
            {
                var res = new LegacySetQuestStateResponse(packet);
                _characterState.UpdateLegacyQuestStateGroup(res.QuestIndex, res.NewState);

                if (res.Result == 0)
                {
                    var state = _characterState.GetLegacyQuestState(res.QuestIndex);
                    var questData = BuildQuestData(res.QuestIndex, state, _characterState, null);

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var scene = MuGame.Instance?.ActiveScene as GameScene;
                        scene?.ChatLog?.AddMessage("System", "Quest: progress updated.", MessageType.System);

                        var questDialog = QuestDialogControl.Instance;
                        questDialog.ShowInfo(questData);
                        AttachNpcCloseOnQuestDialogClosed(questDialog, shouldSendCloseNpc: null);
                    });
                }
                else
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var errorData = new QuestDialogControl.QuestData
                        {
                            QuestIndex = res.QuestIndex,
                            Title = GetQuestTitle(res.QuestIndex),
                            NpcName = GetNpcName(res.QuestIndex),
                            Description = $"Quest action failed (code {res.Result}).\\nPlease check requirements (Zen/items/level).",
                            StateText = "Failed",
                            State = LegacyQuestState.Undefined,
                            CanProceed = false
                        };
                        var questDialog = QuestDialogControl.Instance;
                        questDialog.ShowInfo(errorData);
                        AttachNpcCloseOnQuestDialogClosed(questDialog, shouldSendCloseNpc: null);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LegacySetQuestStateResponse packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xA3, PacketRouter.NoSubCode)] // LegacyQuestReward
        public Task HandleLegacyQuestRewardAsync(Memory<byte> packet)
        {
            try
            {
                var reward = new LegacyQuestReward(packet);
                ushort maskedId = (ushort)(reward.PlayerId & 0x7FFF);

                if (maskedId != (_characterState.Id & 0x7FFF))
                {
                    return Task.CompletedTask;
                }

                string msg = reward.Reward switch
                {
                    LegacyQuestReward.QuestRewardType.LevelUpPoints => $"Quest reward: +{reward.Count} stat points.",
                    LegacyQuestReward.QuestRewardType.CharacterEvolutionFirstToSecond => "Quest reward: class change (1st → 2nd).",
                    LegacyQuestReward.QuestRewardType.LevelUpPointsPerLevelIncrease => $"Quest reward: bonus points per level (value={reward.Count}).",
                    LegacyQuestReward.QuestRewardType.ComboSkill => "Quest reward: combo skill unlocked.",
                    LegacyQuestReward.QuestRewardType.CharacterEvolutionSecondToThird => "Quest reward: class change (2nd → 3rd).",
                    _ => $"Quest reward: {reward.Reward} (value={reward.Count})."
                };

                MuGame.ScheduleOnMainThread(() =>
                {
                    var scene = MuGame.Instance?.ActiveScene as GameScene;
                    scene?.ChatLog?.AddMessage("System", msg, MessageType.System);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LegacyQuestReward packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xA4, 0x00)] // LegacyQuestMonsterKillInfo
        public Task HandleLegacyQuestMonsterKillInfoAsync(Memory<byte> packet)
        {
            try
            {
                var info = new LegacyQuestMonsterKillInfo(packet);
                _logger.LogInformation("LegacyQuestMonsterKillInfo: Result={Result}, QuestIndex={QuestIndex}", info.Result, info.QuestIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing LegacyQuestMonsterKillInfo packet.");
            }

            return Task.CompletedTask;
        }

        private static QuestDialogControl.QuestData BuildQuestData(byte questIndex, LegacyQuestState state, CharacterState characterState, string blockReason)
        {
            var requirements = new List<QuestDialogControl.QuestRequirement>();
            var requiredItems = new List<QuestDialogControl.QuestItem>();

            if (TryGetRequirements(questIndex, out var minLevel, out var minZen, out var prerequisiteQuestIndex, out var prerequisiteState))
            {
                // Level requirement
                if (minLevel > 0)
                {
                    requirements.Add(new QuestDialogControl.QuestRequirement
                    {
                        Label = "Level",
                        CurrentValue = characterState.Level.ToString(),
                        RequiredValue = minLevel.ToString(),
                        IsMet = characterState.Level >= minLevel
                    });
                }

                // Zen requirement
                if (minZen > 0)
                {
                    requirements.Add(new QuestDialogControl.QuestRequirement
                    {
                        Label = "Zen",
                        CurrentValue = FormatZen((uint)characterState.InventoryZen),
                        RequiredValue = FormatZen(minZen),
                        IsMet = characterState.InventoryZen >= minZen
                    });
                }

                // Prerequisite quest requirement
                if (prerequisiteQuestIndex.HasValue)
                {
                    var prereqState = characterState.GetLegacyQuestState(prerequisiteQuestIndex.Value);
                    requirements.Add(new QuestDialogControl.QuestRequirement
                    {
                        Label = $"Quest: {GetQuestTitle(prerequisiteQuestIndex.Value)}",
                        CurrentValue = FormatState(prereqState),
                        RequiredValue = FormatState(prerequisiteState),
                        IsMet = prereqState == prerequisiteState
                    });
                }
            }

            // Get required items for this quest and check inventory
            var questItems = GetQuestItems(questIndex, characterState);
            foreach (var item in questItems)
            {
                item.CurrentCount = CountItemsInInventory(characterState, item.Group, item.Id);
                requiredItems.Add(item);
            }

            return new QuestDialogControl.QuestData
            {
                QuestIndex = questIndex,
                Title = GetQuestTitle(questIndex),
                NpcName = GetNpcName(questIndex),
                Description = GetQuestDescription(questIndex),
                StateText = FormatState(state),
                State = state,
                Requirements = requirements,
                RequiredItems = requiredItems,
                CanProceed = state != LegacyQuestState.Complete && state != LegacyQuestState.Undefined,
                BlockReason = blockReason
            };
        }

        private static string GetQuestTitle(byte questIndex) => questIndex switch
        {
            0 => "Sebina: Scroll of Emperor",
            1 => "Sebina: Three Treasures of Mu",
            2 => "Marlon: Ring of Glory",
            3 => "Marlon: Dark Stone",
            4 => "Devin: Certificate of Strength",
            5 => "Devin: Infiltration of Barracks of Balgass",
            6 => "Devin: Into The Darkness (Balgass Refuge)",
            _ => $"Legacy Quest #{questIndex}"
        };

        private static string GetQuestDescription(byte questIndex) => questIndex switch
        {
            0 =>
                "Pay Zen to hear the story, then hunt and find the Scroll of Emperor.\\n" +
                "Bring it back to Sebina to proceed.",
            1 =>
                "Pay Zen to receive the task.\\n" +
                "Find the appropriate treasure for your class and return to Sebina.\\n" +
                "Reward: class change (1st -> 2nd).",
            2 =>
                "Pay Zen to accept the quest.\\n" +
                "Find the Ring of Glory and return to Marlon.",
            3 =>
                "After completing the Ring of Glory quest, find the Dark Stone and return to Marlon.\\n" +
                "Reward: combo skill (for eligible classes).",
            4 =>
                "Bring the required items to Priest Devin and pay Zen to proceed.",
            5 =>
                "Enter the Barracks of Balgass and defeat the required monsters.\\n" +
                "Reward: additional stat points.",
            6 =>
                "Enter the Refuge of Balgass and defeat the Dark Elf (Hero).\\n" +
                "Reward: class change (2nd -> 3rd) + additional stat points.",
            _ => "No description for this quest in this client."
        };

        private static string GetNpcName(byte questIndex) => questIndex switch
        {
            0 or 1 => "Priestess Sevina",
            2 or 3 => "Marlon",
            4 or 5 or 6 => "Priest Devin",
            _ => "Unknown NPC"
        };

        /// <summary>
        /// Gets the required quest items for a specific quest, based on character class.
        /// These are classic MU Season 6 quest items.
        /// </summary>
        private static List<QuestDialogControl.QuestItem> GetQuestItems(byte questIndex, CharacterState characterState)
        {
            var items = new List<QuestDialogControl.QuestItem>();

            // Quest item group is 14 for most quest items
            const byte questItemGroup = 14;

            switch (questIndex)
            {
                case 0: // Sebina - Scroll of Emperor
                    items.Add(CreateQuestItem(
                        requiredCount: 1,
                        fallbackGroup: questItemGroup,
                        fallbackId: 19,
                        "Scroll of the Emperor",
                        "Scroll of Emperor"));
                    break;

                case 1: // Sebina - Three Treasures (class-specific)
                    // Get class-specific treasure item
                    var classItem = GetClassSpecificTreasure(characterState);
                    if (classItem != null)
                    {
                        items.Add(classItem);
                    }
                    break;

                case 2: // Marlon - Ring of Glory
                    items.Add(CreateQuestItem(
                        requiredCount: 1,
                        fallbackGroup: 13,
                        fallbackId: 20,
                        "Ring of Glory",
                        "Ring of Honor"));
                    break;

                case 3: // Marlon - Dark Stone
                    items.Add(CreateQuestItem(
                        requiredCount: 1,
                        fallbackGroup: questItemGroup,
                        fallbackId: 31,
                        "Dark Stone"));
                    break;

                case 4: // Devin - Certificate of Strength (items vary by server)
                    // Common items: Flame of Death Beam Knight or similar
                    items.Add(CreateQuestItem(
                        requiredCount: 1,
                        fallbackGroup: questItemGroup,
                        fallbackId: 20,
                        "Flame of Death Beam Knight",
                        "Death-beam Knight Flame",
                        "Death Beam Knight Flame"));
                    items.Add(CreateQuestItem(
                        requiredCount: 1,
                        fallbackGroup: questItemGroup,
                        fallbackId: 21,
                        "Horn of Hell Maine",
                        "Hell-Miner Horn"));
                    items.Add(CreateQuestItem(
                        requiredCount: 1,
                        fallbackGroup: questItemGroup,
                        fallbackId: 22,
                        "Feather of Dark Phoenix",
                        "Dark Phoenix Feather"));
                    break;

                // Quests 5 and 6 are monster-kill quests, no items required
            }

            return items;
        }

        private static QuestDialogControl.QuestItem CreateQuestItem(int requiredCount, byte fallbackGroup, short fallbackId, params string[] candidateNames)
        {
            if (candidateNames != null)
            {
                foreach (string name in candidateNames)
                {
                    if (ItemDatabase.TryGetItemDefinitionByName(name, out var def) && def != null)
                    {
                        return new QuestDialogControl.QuestItem
                        {
                            Name = def.Name,
                            Group = (byte)def.Group,
                            Id = (short)def.Id,
                            RequiredCount = requiredCount
                        };
                    }
                }
            }

            return new QuestDialogControl.QuestItem
            {
                Name = candidateNames?.FirstOrDefault() ?? "Unknown Item",
                Group = fallbackGroup,
                Id = fallbackId,
                RequiredCount = requiredCount
            };
        }

        /// <summary>
        /// Gets the class-specific treasure item for the Sebina quest (quest 1).
        /// </summary>
        private static QuestDialogControl.QuestItem GetClassSpecificTreasure(CharacterState characterState)
        {
            const byte questItemGroup = 14;

            // CharacterClassNumber values are not grouped by 0x10 (e.g. DarkKnight=4, BladeKnight=6, ...).
            // Use the enum directly to avoid mis-detecting class tribe (DK showing wizard items etc).
            return characterState.Class switch
            {
                CharacterClassNumber.DarkWizard or
                CharacterClassNumber.SoulMaster or
                CharacterClassNumber.GrandMaster
                    => CreateQuestItem(1, questItemGroup, 18,
                        "Soul of Wizard",
                        "Soul Shard of Wizard",
                        "Soul of the Wizard",
                        "Soul Shard of the Wizard"),

                CharacterClassNumber.DarkKnight or
                CharacterClassNumber.BladeKnight or
                CharacterClassNumber.BladeMaster
                    => CreateQuestItem(1, questItemGroup, 17, "Broken Sword"),

                CharacterClassNumber.FairyElf or
                CharacterClassNumber.MuseElf or
                CharacterClassNumber.HighElf
                    => CreateQuestItem(1, questItemGroup, 16, "Tear of Elf", "Tear of the Elf"),

                CharacterClassNumber.Summoner or
                CharacterClassNumber.BloodySummoner or
                CharacterClassNumber.DimensionMaster
                    => CreateQuestItem(1, questItemGroup, 68, "Abyssal Eye", "Eye of Abyssal"),

                _ => null
            };
        }

        /// <summary>
        /// Counts how many items of a specific type are in the character's inventory.
        /// </summary>
        private static int CountItemsInInventory(CharacterState characterState, byte group, short id)
        {
            var inventory = characterState.GetInventoryItems();
            int count = 0;

            foreach (var kv in inventory)
            {
                var itemData = kv.Value;
                if (itemData == null || itemData.Length < 6)
                    continue;

                short itemId = itemData[0];
                byte itemGroup = (byte)(itemData[5] >> 4);

                if (itemGroup == group && itemId == id)
                {
                    // For stackable items, durability represents count
                    byte durability = itemData.Length > 2 ? itemData[2] : (byte)1;
                    count += durability > 0 ? durability : 1;
                }
            }

            return count;
        }

        /// <summary>
        /// Checks all requirements including level, zen, prerequisite quest, and required items (only for Active quests).
        /// For Inactive quests, only check prerequisites (level/zen/prerequisite quest), not items.
        /// For Active quests, also check required items.
        /// </summary>
        private static bool CheckAllRequirements(byte questIndex, CharacterState characterState, out string blockReason)
        {
            blockReason = null;

            // Check basic requirements (level, zen, prerequisite)
            if (!CheckRequirements(questIndex, characterState, out var basicError))
            {
                blockReason = basicError;
                return false;
            }

            // Only check required items for Active quests (already accepted, need to complete)
            // For Inactive quests (not yet accepted), don't check items
            var state = characterState.GetLegacyQuestState(questIndex);
            if (state == LegacyQuestState.Active)
            {
                var requiredItems = GetQuestItems(questIndex, characterState);
                foreach (var item in requiredItems)
                {
                    int count = CountItemsInInventory(characterState, item.Group, item.Id);
                    if (count < item.RequiredCount)
                    {
                        blockReason = $"Missing item: {item.Name}";
                        return false;
                    }
                }
            }

            return true;
        }

        private static string FormatState(LegacyQuestState state) => state switch
        {
            LegacyQuestState.Inactive => "Inactive",
            LegacyQuestState.Active => "In progress",
            LegacyQuestState.Complete => "Completed",
            LegacyQuestState.Undefined => "Unavailable",
            _ => state.ToString()
        };

        private static bool CheckRequirements(byte questIndex, CharacterState characterState, out string error)
        {
            error = string.Empty;
            if (!TryGetRequirements(questIndex, out var minLevel, out var minZen, out var prerequisiteQuestIndex, out var prerequisiteState))
            {
                return true;
            }

            if (minLevel > 0 && characterState.Level < minLevel)
            {
                error = $"Requirement not met: Level {minLevel}+ required.";
                return false;
            }

            if (minZen > 0 && characterState.InventoryZen < minZen)
            {
                error = $"Requirement not met: {FormatZen(minZen)} Zen required.";
                return false;
            }

            if (prerequisiteQuestIndex.HasValue)
            {
                var st = characterState.GetLegacyQuestState(prerequisiteQuestIndex.Value);
                if (st != prerequisiteState)
                {
                    error = $"Requirement not met: {GetQuestTitle(prerequisiteQuestIndex.Value)} must be {FormatState(prerequisiteState)}.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetRequirements(
            byte questIndex,
            out ushort minLevel,
            out uint minZen,
            out byte? prerequisiteQuestIndex,
            out LegacyQuestState prerequisiteState)
        {
            minLevel = 0;
            minZen = 0;
            prerequisiteQuestIndex = null;
            prerequisiteState = LegacyQuestState.Complete;

            // These are the classic values used by the original client for the legacy class change quests.
            // Servers can vary, but client-side checks should match the common S6 flow.
            switch (questIndex)
            {
                case 0: // Sebina - story token
                    minLevel = 150;
                    minZen = 1_000_000;
                    return true;
                case 1: // Sebina - treasures
                    minLevel = 150;
                    minZen = 2_000_000;
                    prerequisiteQuestIndex = 0;
                    prerequisiteState = LegacyQuestState.Complete;
                    return true;
                case 2: // Marlon - ring
                    minZen = 3_000_000;
                    return true;
                case 3: // Marlon - dark stone / combo
                    minZen = 3_000_000;
                    prerequisiteQuestIndex = 2;
                    prerequisiteState = LegacyQuestState.Complete;
                    return true;
                case 4: // Devin part 1
                    minLevel = 380;
                    minZen = 5_000_000;
                    return true;
                case 5: // Devin part 2
                    minLevel = 400;
                    minZen = 7_000_000;
                    prerequisiteQuestIndex = 4;
                    prerequisiteState = LegacyQuestState.Complete;
                    return true;
                case 6: // Devin part 3
                    minLevel = 400;
                    minZen = 10_000_000;
                    prerequisiteQuestIndex = 5;
                    prerequisiteState = LegacyQuestState.Complete;
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatZen(uint zen)
        {
            if (zen >= 1_000_000_000) return $"{zen / 1_000_000_000d:0.##}B";
            if (zen >= 1_000_000) return $"{zen / 1_000_000d:0.##}M";
            if (zen >= 1_000) return $"{zen / 1_000d:0.##}K";
            return zen.ToString();
        }

        private void AttachNpcCloseOnQuestDialogClosed(QuestDialogControl dialog, Func<bool> shouldSendCloseNpc)
        {
            if (dialog == null)
            {
                return;
            }

            // This dialog exists because the server opened an NPC dialog (F9) and sent a legacy quest dialog (A1).
            // Ensure we always inform the server when the user closes our UI.
            void OnDialogClosed()
            {
                dialog.DialogClosed -= OnDialogClosed;

                if (shouldSendCloseNpc?.Invoke() == false)
                {
                    return;
                }

                _ = _characterService.SendCloseNpcRequestAsync();
            }

            dialog.DialogClosed += OnDialogClosed;
        }
    }
}
