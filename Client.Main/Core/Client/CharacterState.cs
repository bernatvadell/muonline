using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;

namespace Client.Main.Core.Client
{
    /// <summary>
    /// Represents the state of a learned skill, including its ID, level, and display values.
    /// </summary>
    public class SkillEntryState
    {
        /// <summary>
        /// Gets or sets the unique identifier of the skill.
        /// </summary>
        public ushort SkillId { get; set; }
        /// <summary>
        /// Gets or sets the current level of the skill.
        /// </summary>
        public byte SkillLevel { get; set; }
        /// <summary>
        /// Gets or sets the current display value of the skill, if applicable.
        /// This could represent a percentage or a numerical value shown to the player.
        /// </summary>
        public float? DisplayValue { get; set; }
        /// <summary>
        /// Gets or sets the next display value of the skill, often shown in tooltips to indicate the value after leveling up.
        /// </summary>
        public float? NextDisplayValue { get; set; }
    }

    /// <summary>
    /// Holds the state of the currently logged-in character, including basic info, stats, inventory, and skills.
    /// This class is responsible for tracking and updating the character's attributes as received from the server.
    /// </summary>
    public class CharacterState
    {
        private readonly ILogger<CharacterState> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterState"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory for creating a logger.</param>
        public CharacterState(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CharacterState>();
        }

        // Basic Character Information
        public string Name { get; set; } = "???";
        public ushort Id { get; set; } = 0xFFFF;
        public bool IsInGame { get; set; } = false;
        public CharacterClassNumber Class { get; set; } = CharacterClassNumber.DarkWizard;
        public CharacterStatus Status { get; set; } = CharacterStatus.Normal;
        public CharacterHeroState HeroState { get; set; } = CharacterHeroState.Normal;

        // Level and Experience Information
        public ushort Level { get; set; } = 1;
        public ulong Experience { get; set; } = 0;
        public ulong ExperienceForNextLevel { get; set; } = 1;
        public ushort LevelUpPoints { get; set; } = 0;
        public ushort MasterLevel { get; set; } = 0;
        public ulong MasterExperience { get; set; } = 0;
        public ulong MasterExperienceForNextLevel { get; set; } = 1;
        public ushort MasterLevelUpPoints { get; set; } = 0;

        // Position and Map Information
        public byte PositionX { get; set; } = 0;
        public byte PositionY { get; set; } = 0;
        public ushort MapId { get; set; } = 0;
        public byte Direction { get; set; } = 0; // Default direction, e.g., West

        // Core Stats (HP, Mana, SD, AG)
        public uint CurrentHealth { get; set; } = 0;
        public uint MaximumHealth { get; set; } = 1;
        public uint CurrentShield { get; set; } = 0;
        public uint MaximumShield { get; set; } = 0;
        public uint CurrentMana { get; set; } = 0;
        public uint MaximumMana { get; set; } = 1;
        public uint CurrentAbility { get; set; } = 0;
        public uint MaximumAbility { get; set; } = 0;

        // Base Stats (Strength, Agility, Vitality, Energy, Leadership)
        public ushort Strength { get; set; } = 0;
        public ushort Agility { get; set; } = 0;
        public ushort Vitality { get; set; } = 0;
        public ushort Energy { get; set; } = 0;
        public ushort Leadership { get; set; } = 0;

        // Inventory and Money
        private readonly ConcurrentDictionary<byte, byte[]> _inventoryItems = new();
        public byte InventoryExpansionState { get; set; } = 0;
        public uint InventoryZen { get; set; } = 0;

        // Constants for Item Data Parsing (based on ItemSerializer.cs, Season 6 assumed)
        private const byte LuckFlagBit = 4;
        private const byte SkillFlagBit = 128;
        private const byte LevelMask = 0x78;
        private const byte LevelShift = 3;
        private const byte OptionLevelMask = 0x03;
        private const byte Option3rdBitShift = 4; // For the 3rd option level bit in ExcByte
        private const byte Option3rdBitMask = 0x40; // The 3rd option level bit in ExcByte
        private const byte GuardianOptionFlag = 0x08; // In Item Group Byte
        private const byte AncientBonusLevelMask = 0b1100;
        private const byte AncientBonusLevelShift = 2;
        private const byte AncientDiscriminatorMask = 0b0011;

        // Excellent Option Bits (in ExcByte)
        private const byte ExcManaInc = 0b0000_0001;
        private const byte ExcLifeInc = 0b0000_0010;
        private const byte ExcDmgInc = 0b0000_0100;
        private const byte ExcSpeedInc = 0b0000_1000;
        private const byte ExcRateInc = 0b0001_0000;
        private const byte ExcZenInc = 0b0010_0000;

        // Skills
        private readonly ConcurrentDictionary<ushort, SkillEntryState> _skillList = new();

        // --- Update Methods ---

        public uint CurrentHp => CurrentHealth;
        public uint MaxHp => MaximumHealth;
        public uint CurrentMp => CurrentMana;
        public uint MaxMp => MaximumMana;

        public event Action<uint, uint> HealthChanged; // (current, max)
        public event Action<uint, uint> ManaChanged;

        public ushort AddedStrength { get; set; } = 0;
        public ushort AddedAgility { get; set; } = 0;
        public ushort AddedVitality { get; set; } = 0;
        public ushort AddedEnergy { get; set; } = 0;
        public ushort AddedLeadership { get; set; } = 0;

        public ushort TotalStrength => (ushort)(Strength + AddedStrength);
        public ushort TotalAgility => (ushort)(Agility + AddedAgility);
        public ushort TotalVitality => (ushort)(Vitality + AddedVitality);
        public ushort TotalEnergy => (ushort)(Energy + AddedEnergy);
        public ushort TotalLeadership => (ushort)(Leadership + AddedLeadership);

        private void RaiseHealth() => HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        private void RaiseMana() => ManaChanged?.Invoke(CurrentMana, MaximumMana);

        /// <summary>
        /// Updates core character identification data. More detailed stats (HP, MP, attributes etc.)
        /// are expected to be updated by specific server packets like CharacterInformation or individual stat updates.
        /// </summary>
        public void UpdateCoreCharacterInfo(
            ushort id,
            string name,
            CharacterClassNumber characterClass,
            ushort level,
            byte posX, byte posY, ushort mapId)
        {
            _logger.LogInformation("Updating CORE CharacterState for: {Name}, Level: {Level}, Class: {Class}", name, level, characterClass);

            if (id != 0 && id != 0xFFFF) this.Id = id;
            this.Name = name;
            this.Class = characterClass;
            this.Level = level;
            this.PositionX = posX;
            this.PositionY = posY;
            this.MapId = mapId;
            this.IsInGame = true;

            _logger.LogInformation("CORE CharacterState updated for {Name}. HP/MP/Stats/Zen will be updated by subsequent packets.", name);
        }

        public void UpdateCurrentHealthShield(uint currentHealth, uint currentShield)
        {
            CurrentHealth = currentHealth;
            CurrentShield = currentShield;
            RaiseHealth();
            _logger.LogInformation("❤️ HP: {Current}/{Max} | 🛡️ SD: {Shield}/{MaxShield}",
                                   CurrentHealth, MaximumHealth, CurrentShield, MaximumShield);
        }

        public void UpdateMaximumHealthShield(uint maximumHealth, uint maximumShield)
        {
            MaximumHealth = Math.Max(1, maximumHealth);
            MaximumShield = maximumShield;
            RaiseHealth();
            _logger.LogInformation("❤️ Max HP: {Max} | 🛡️ Max SD: {MaxShield}",
                                   MaximumHealth, MaximumShield);
        }

        public void UpdateCurrentManaAbility(uint currentMana, uint currentAbility)
        {
            CurrentMana = currentMana;
            CurrentAbility = currentAbility;
            RaiseMana();
            _logger.LogInformation("💧 Mana: {Current}/{Max} | ✨ AG: {Ability}/{MaxAbility}",
                                   CurrentMana, MaximumMana, CurrentAbility, MaximumAbility);
        }

        public void UpdateMaximumManaAbility(uint maximumMana, uint maximumAbility)
        {
            MaximumMana = Math.Max(1, maximumMana);
            MaximumAbility = maximumAbility;
            RaiseMana();
            _logger.LogInformation("💧 Max Mana: {Max} | ✨ Max AG: {MaxAbility}",
                                   MaximumMana, MaximumAbility);
        }

        /// <summary>
        /// Updates the character's position coordinates.
        /// </summary>
        public void UpdatePosition(byte x, byte y)
        {
            PositionX = x;
            PositionY = y;
            _logger.LogDebug("Character position updated to X: {X}, Y: {Y}", x, y);
        }

        /// <summary>
        /// Updates the character's direction.
        /// </summary>
        public void UpdateDirection(byte direction)
        {
            Direction = direction;
            _logger.LogDebug("Character direction updated to: {Direction}", direction);
        }

        /// <summary>
        /// Updates the character's current map ID.
        /// </summary>
        public void UpdateMap(ushort mapId)
        {
            MapId = mapId;
            _logger.LogInformation("Character map changed to ID: {MapId}", mapId);
        }

        /// <summary>
        /// Updates the character's level, experience, and level up points.
        /// </summary>
        public void UpdateLevelAndExperience(ushort level, ulong currentExperience, ulong nextLevelExperience, ushort levelUpPoints)
        {
            Level = level;
            Experience = currentExperience;
            ExperienceForNextLevel = Math.Max(1, nextLevelExperience);
            LevelUpPoints = levelUpPoints;
            _logger.LogInformation("Level and experience updated. Level: {Level}, Exp: {Experience}, Next Level Exp: {NextLevelExperience}, LevelUpPoints: {LevelUpPoints}", level, currentExperience, nextLevelExperience, levelUpPoints);
        }

        /// <summary>
        /// Updates the character's master level, master experience, and master level up points.
        /// </summary>
        public void UpdateMasterLevelAndExperience(ushort masterLevel, ulong currentMasterExperience, ulong nextMasterLevelExperience, ushort masterLevelUpPoints)
        {
            MasterLevel = masterLevel;
            MasterExperience = currentMasterExperience;
            MasterExperienceForNextLevel = Math.Max(1, nextMasterLevelExperience);
            MasterLevelUpPoints = masterLevelUpPoints;
            _logger.LogInformation("Master level and experience updated. Master Level: {MasterLevel}, Master Exp: {MasterExperience}, Next Master Level Exp: {NextMasterLevelExperience}, Master LevelUpPoints: {MasterLevelUpPoints}", masterLevel, currentMasterExperience, nextMasterLevelExperience, masterLevelUpPoints);
        }

        /// <summary>
        /// Adds experience points to the character's current experience.
        /// </summary>
        public void AddExperience(uint addedExperience)
        {
            Experience += addedExperience;
            _logger.LogDebug("Added experience: {AddedExperience}. Total Experience: {Experience}", addedExperience, Experience);
        }

        /// <summary>
        /// Updates the character's base stats (Strength, Agility, Vitality, Energy, Leadership).
        /// </summary>
        public void UpdateStats(ushort strength, ushort agility, ushort vitality, ushort energy, ushort leadership)
        {
            Strength = strength;
            Agility = agility;
            Vitality = vitality;
            Energy = energy;
            Leadership = leadership;
            _logger.LogInformation("📊 Stats: Str={Str}, Agi={Agi}, Vit={Vit}, Ene={Ene}, Cmd={Cmd}",
                Strength, Agility, Vitality, Energy, Leadership);
        }

        /// <summary>
        /// Increments a specific character stat attribute by a given amount.
        /// </summary>
        public void IncrementStat(CharacterStatAttribute attribute, ushort amount = 1)
        {
            switch (attribute)
            {
                case CharacterStatAttribute.Strength:
                    Strength += amount;
                    break;
                case CharacterStatAttribute.Agility:
                    Agility += amount;
                    break;
                case CharacterStatAttribute.Vitality:
                    Vitality += amount;
                    break;
                case CharacterStatAttribute.Energy:
                    Energy += amount;
                    break;
                case CharacterStatAttribute.Leadership:
                    Leadership += amount;
                    break;
            }
            _logger.LogDebug("Incremented stat {Attribute} by {Amount}", attribute, amount);
        }

        /// <summary>
        /// Updates the amount of Zen in the character's inventory.
        /// </summary>
        public void UpdateInventoryZen(uint zen)
        {
            InventoryZen = zen;
            _logger.LogDebug("Inventory Zen updated to: {Zen}", zen);
        }

        /// <summary>
        /// Updates the character's status and hero state.
        /// </summary>
        public void UpdateStatus(CharacterStatus status, CharacterHeroState heroState)
        {
            Status = status;
            HeroState = heroState;
            _logger.LogInformation("Character status updated. Status: {Status}, Hero State: {HeroState}", status, heroState);
        }

        /// <summary>
        /// Clears all items from the character's inventory.
        /// </summary>
        public void ClearInventory()
        {
            _inventoryItems.Clear();
            _logger.LogDebug("Inventory cleared.");
        }

        /// <summary>
        /// Adds or updates an item in the character's inventory at a specific slot.
        /// </summary>
        public void AddOrUpdateInventoryItem(byte slot, byte[] itemData)
        {
            _inventoryItems[slot] = itemData;
            string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
            _logger.LogDebug("Inventory item added/updated at slot {Slot}: {ItemName}", slot, itemName);
        }

        /// <summary>
        /// Removes an item from the character's inventory at a specific slot.
        /// </summary>
        public void RemoveInventoryItem(byte slot)
        {
            bool removed = _inventoryItems.TryRemove(slot, out _);
            if (removed)
            {
                _logger.LogDebug("Inventory item removed from slot {Slot}", slot);
            }
            else
            {
                _logger.LogWarning("Attempted to remove inventory item from slot {Slot}, but no item found.", slot);
            }
        }

        /// <summary>
        /// Updates the durability of an item in the inventory at a specific slot.
        /// Assumes durability is at index 2 in the item data byte array.
        /// </summary>
        public void UpdateItemDurability(byte slot, byte durability)
        {
            if (_inventoryItems.TryGetValue(slot, out byte[] itemData))
            {
                const int durabilityIndex = 2;
                if (itemData.Length > durabilityIndex)
                {
                    itemData[durabilityIndex] = durability;
                    _logger.LogDebug("Item durability updated for slot {Slot} to {Durability}", slot, durability);
                }
                else
                {
                    _logger.LogWarning("Could not update item durability for slot {Slot}, item data too short.", slot);
                }
            }
            else
            {
                _logger.LogWarning("Could not update item durability for slot {Slot}, item not found.", slot);
            }
        }

        // --- Skill List Methods ---

        /// <summary>
        /// Clears the character's skill list.
        /// </summary>
        public void ClearSkillList()
        {
            _skillList.Clear();
            _logger.LogDebug("Skill list cleared.");
        }

        /// <summary>
        /// Adds or updates a skill in the character's skill list.
        /// </summary>
        public void AddOrUpdateSkill(SkillEntryState skill)
        {
            _skillList[skill.SkillId] = skill;
            _logger.LogDebug("Skill added/updated. Skill ID: {SkillId}, Level: {SkillLevel}", skill.SkillId, skill.SkillLevel);
        }

        /// <summary>
        /// Removes a skill from the character's skill list by its skill ID.
        /// </summary>
        public void RemoveSkill(ushort skillId)
        {
            bool removed = _skillList.TryRemove(skillId, out _);
            if (removed)
            {
                _logger.LogDebug("Skill removed. Skill ID: {SkillId}", skillId);
            }
            else
            {
                _logger.LogWarning("Attempted to remove skill with ID {SkillId}, but skill not found.", skillId);
            }
        }

        /// <summary>
        /// Gets all skills in the skill list as an enumerable collection, ordered by skill ID.
        /// </summary>
        public IEnumerable<SkillEntryState> GetSkills()
        {
            return _skillList.Values.OrderBy(s => s.SkillId);
        }

        // --- Display Methods ---

        /// <summary>
        /// Gets a formatted string representation of the character's inventory.
        /// </summary>
        public string GetInventoryDisplay()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n--- Inventory ---");
            sb.AppendLine($"  Zen: {InventoryZen:N0}");
            if (_inventoryItems.IsEmpty)
            {
                sb.AppendLine(" (Inventory is empty)");
            }
            else
            {
                foreach (var kvp in _inventoryItems.OrderBy(kv => kv.Key))
                {
                    byte slot = kvp.Key;
                    byte[] itemData = kvp.Value;
                    string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                    string itemDetails = ParseItemDetails(itemData);
                    sb.AppendLine($"  Slot {slot,3}: {itemName}{itemDetails}");
                }
            }
            sb.AppendLine($"  Expansion State: {InventoryExpansionState}");
            sb.AppendLine("-----------------\n");
            return sb.ToString();
        }

        /// <summary>
        /// Gets a list of key-value pairs representing character stats for UI display.
        /// </summary>
        public List<KeyValuePair<string, string>> GetFormattedStatsList()
        {
            var stats = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Name", Name),
                new KeyValuePair<string, string>("Class", CharacterClassDatabase.GetClassName(Class)),
                new KeyValuePair<string, string>("Level", $"{Level} ({LevelUpPoints} Points)"),
            };

            if (MasterLevel > 0)
            {
                stats.Add(new KeyValuePair<string, string>("Master Level", $"{MasterLevel} ({MasterLevelUpPoints} Points)"));
                stats.Add(new KeyValuePair<string, string>("Exp", $"{Experience:N0} / {ExperienceForNextLevel:N0}"));
                stats.Add(new KeyValuePair<string, string>("M.Exp", $"{MasterExperience:N0} / {MasterExperienceForNextLevel:N0}"));
            }
            else
            {
                stats.Add(new KeyValuePair<string, string>("Exp", $"{Experience:N0} / {ExperienceForNextLevel:N0}"));
            }

            stats.Add(new KeyValuePair<string, string>("Map", $"{MapDatabase.GetMapName(MapId)} ({MapId})"));
            stats.Add(new KeyValuePair<string, string>("Position", $"({PositionX},{PositionY})"));
            stats.Add(new KeyValuePair<string, string>("Status", $"{Status}"));
            stats.Add(new KeyValuePair<string, string>("Hero State", $"{HeroState}"));
            stats.Add(new KeyValuePair<string, string>("HP", $"{CurrentHealth}/{MaximumHealth}"));
            stats.Add(new KeyValuePair<string, string>("Mana", $"{CurrentMana}/{MaximumMana}"));
            stats.Add(new KeyValuePair<string, string>("SD", $"{CurrentShield}/{MaximumShield}"));
            stats.Add(new KeyValuePair<string, string>("AG", $"{CurrentAbility}/{MaximumAbility}"));
            stats.Add(new KeyValuePair<string, string>("Strength", $"{Strength}"));
            stats.Add(new KeyValuePair<string, string>("Agility", $"{Agility}"));
            stats.Add(new KeyValuePair<string, string>("Vitality", $"{Vitality}"));
            stats.Add(new KeyValuePair<string, string>("Energy", $"{Energy}"));
            stats.Add(new KeyValuePair<string, string>("Command", $"{Leadership}"));

            return stats;
        }


        /// <summary>
        /// Gets a formatted string representation of the character's stats.
        /// </summary>
        public string GetStatsDisplay()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n--- Character Stats ---");
            sb.AppendLine($"  Name: {Name} (ID: {Id:X4})");
            sb.AppendLine($"  Class: {CharacterClassDatabase.GetClassName(Class)}");
            sb.AppendLine($"  Level: {Level} ({LevelUpPoints} Points)");
            if (MasterLevel > 0)
            {
                sb.AppendLine($"  Master Level: {MasterLevel} ({MasterLevelUpPoints} Points)");
            }
            sb.AppendLine($"  Exp: {Experience:N0} / {ExperienceForNextLevel:N0}");
            if (MasterLevel > 0)
            {
                sb.AppendLine($"  M.Exp: {MasterExperience:N0} / {MasterExperienceForNextLevel:N0}");
            }
            sb.AppendLine($"  Map: {MapDatabase.GetMapName(MapId)} ({MapId}) at ({PositionX},{PositionY})");
            sb.AppendLine($"  Status: {Status}, Hero State: {HeroState}");
            sb.AppendLine($"  HP: {CurrentHealth}/{MaximumHealth}");
            sb.AppendLine($"  Mana: {CurrentMana}/{MaximumMana}");
            sb.AppendLine($"  SD: {CurrentShield}/{MaximumShield}");
            sb.AppendLine($"  AG: {CurrentAbility}/{MaximumAbility}");
            sb.AppendLine($"  Strength: {Strength}");
            sb.AppendLine($"  Agility: {Agility}");
            sb.AppendLine($"  Vitality: {Vitality}");
            sb.AppendLine($"  Energy: {Energy}");
            sb.AppendLine($"  Command: {Leadership}");
            sb.AppendLine("-----------------------\n");
            return sb.ToString();
        }

        /// <summary>
        /// Gets a read-only dictionary representation of the current inventory items.
        /// Key is the slot number, Value is the raw item data.
        /// </summary>
        public IReadOnlyDictionary<byte, byte[]> GetInventoryItems()
        {
            // Return a copy or read-only view for thread safety
            return new ReadOnlyDictionary<byte, byte[]>(_inventoryItems.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        /// <summary>
        /// Formats item data from a specific slot into a display string.
        /// </summary>
        public string FormatInventoryItem(byte slot, byte[] itemData)
        {
            string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
            string itemDetails = ParseItemDetails(itemData);
            return $"Slot {slot,3}: {itemName}{itemDetails}";
        }

        /// <summary>
        /// Gets a formatted string representation of the character's skill list.
        /// </summary>
        public string GetSkillListDisplay()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n--- Skill List ---");
            if (_skillList.IsEmpty)
            {
                sb.AppendLine(" (No skills learned/equipped)");
            }
            else
            {
                foreach (var skill in GetSkills())
                {
                    sb.Append($"  ID: {skill.SkillId,-5} Level: {skill.SkillLevel,-2}");
                    if (skill.DisplayValue.HasValue)
                    {
                        sb.Append($" Value: {skill.DisplayValue.Value:F1}");
                    }
                    if (skill.NextDisplayValue.HasValue)
                    {
                        sb.Append($" Next: {skill.NextDisplayValue.Value:F1}");
                    }
                    sb.AppendLine();
                }
            }
            sb.AppendLine("------------------\n");
            return sb.ToString();
        }

        // --- Item Data Parsing Helpers ---

        /// <summary>
        /// Parses item data bytes based on common item structure (Season 6 assumed).
        /// Extracts item level, skill, luck, option, excellent options, ancient options, level 380 option, harmony option, socket bonus, and socket options.
        /// </summary>
        /// <param name="itemData">Byte array of item data (typically 8, 12, or more bytes).</param>
        /// <returns>String containing parsed item details, or an empty string if no details are parsed.</returns>
        private string ParseItemDetails(byte[] itemData)
        {
            if (itemData == null) return " (Null Data)";
            int dataLength = itemData.Length;
            if (dataLength < 3) return " (Data Too Short)";

            var details = new StringBuilder();
            byte optionLevelByte = itemData[1]; // Contains level, skill, luck, option bits 0-1
            byte durability = itemData[2];
            byte excByte = dataLength > 3 ? itemData[3] : (byte)0; // Contains excellent options + option bit 2 + item group high bit
            byte ancientByte = dataLength > 4 ? itemData[4] : (byte)0; // Contains ancient set info
            byte groupByte = dataLength > 5 ? itemData[5] : (byte)0; // Contains item group high nibble + 380 option flag
            byte harmonyByte = dataLength > 6 ? itemData[6] : (byte)0; // Contains harmony type + socket bonus type
            byte harmonyLevelByte = dataLength > 7 ? itemData[7] : (byte)0; // Contains harmony level

            // Level
            int level = (optionLevelByte & LevelMask) >> LevelShift;
            if (level > 0) details.Append($" +{level}");

            // Skill
            if ((optionLevelByte & SkillFlagBit) != 0) details.Append(" +Skill");

            // Luck
            if ((optionLevelByte & LuckFlagBit) != 0) details.Append(" +Luck");

            // Option
            int optionLevel = (optionLevelByte & OptionLevelMask);
            if ((excByte & Option3rdBitMask) != 0) optionLevel |= 0b100; // Add the 3rd bit
            if (optionLevel > 0) details.Append($" +{optionLevel * 4} Opt");

            // Excellent Options
            var excellentOptions = ParseExcellentOptions(excByte);
            if (excellentOptions.Any()) details.Append($" +Exc({string.Join(",", excellentOptions)})");

            // Ancient Options
            if ((ancientByte & 0x0F) > 0)
            {
                int setId = ancientByte & AncientDiscriminatorMask;
                int bonusLevel = (ancientByte & AncientBonusLevelMask) >> AncientBonusLevelShift;
                details.Append($" +Anc(Set:{setId}");
                if (bonusLevel > 0) details.Append($",Lvl:{bonusLevel}");
                details.Append(")");
            }

            // Level 380 Option (PvP Option)
            if ((groupByte & GuardianOptionFlag) != 0) details.Append(" +PvP");

            // Harmony Option
            byte harmonyType = (byte)(harmonyByte & 0xF0);
            byte harmonyLevel = harmonyLevelByte;
            if (harmonyType > 0)
            {
                // Placeholder: Mapping harmonyType to name requires a lookup table
                details.Append($" +Har(T:{(harmonyType >> 4)},L:{harmonyLevel})");
            }

            // Socket Bonus Option
            byte socketBonusType = (byte)(harmonyByte & 0x0F);
            if (socketBonusType > 0)
            {
                // Placeholder: Mapping socketBonusType to name requires a lookup table
                details.Append($" +SockBonus({socketBonusType})");
            }

            // Socket Options (Assuming S6 item structure)
            if (dataLength >= 12) // Need up to byte 11 for 5 sockets
            {
                var sockets = new List<string>();
                // Socket bytes are typically at indices 7, 8, 9, 10, 11
                for (int i = 7; i < Math.Min(dataLength, 12); i++)
                {
                    byte socketByte = itemData[i];
                    if (socketByte != 0xFF && socketByte != 0xFE)
                    {
                        sockets.Add(ParseSocketOption(socketByte)); // 0xFF=Empty, 0xFE=No Socket
                    }
                }
                if (sockets.Any()) details.Append($" +Socket({string.Join(",", sockets)})");
            }

            // Durability
            details.Append($" (Dur: {durability})");

            string result = details.ToString().Trim();
            return string.IsNullOrEmpty(result) ? string.Empty : $" {result}";
        }

        /// <summary>
        /// Parses the excellent options byte to extract enabled excellent options flags.
        /// </summary>
        /// <param name="excByte">The excellent options byte (typically itemData[3] in S6).</param>
        /// <returns>A list of strings representing the enabled excellent options.</returns>
        private List<string> ParseExcellentOptions(byte excByte)
        {
            var options = new List<string>();
            if ((excByte & ExcManaInc) != 0) options.Add("MP/8");
            if ((excByte & ExcLifeInc) != 0) options.Add("HP/8");
            if ((excByte & ExcSpeedInc) != 0) options.Add("Speed");
            if ((excByte & ExcDmgInc) != 0) options.Add("Dmg%");
            if ((excByte & ExcRateInc) != 0) options.Add("Rate");
            if ((excByte & ExcZenInc) != 0) options.Add("Zen");
            return options;
        }

        /// <summary>
        /// Parses a socket option byte into a readable string (placeholder).
        /// Requires a lookup table mapping byte values to seed sphere types.
        /// </summary>
        /// <param name="socketByte">The socket option byte.</param>
        /// <returns>A string representing the socket option (placeholder).</returns>
        private string ParseSocketOption(byte socketByte)
        {
            // TODO: Implement mapping from socketByte value to actual Seed Sphere name/effect
            return $"S:{socketByte}"; // Placeholder
        }
    }
}