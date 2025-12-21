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
    /// Represents an active buff/effect on the character.
    /// </summary>
    public class ActiveBuffState
    {
        /// <summary>
        /// Gets or sets the effect ID (from MagicEffectStatus packet).
        /// </summary>
        public byte EffectId { get; set; }

        /// <summary>
        /// Gets or sets the raw player ID this effect is active on.
        /// </summary>
        public ushort PlayerIdRaw { get; set; }

        /// <summary>
        /// Gets or sets the masked player ID (ID &amp; 0x7FFF).
        /// </summary>
        public ushort PlayerIdMasked { get; set; }

        /// <summary>
        /// Gets or sets when this effect was activated.
        /// </summary>
        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
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

        // Combat speeds (provided by server, Season 6)
        public ushort AttackSpeed { get; private set; } = 0;
        public ushort MagicSpeed { get; private set; } = 0;
        public ushort MaximumAttackSpeed { get; private set; } = 0;

        // Inventory and Money
        private readonly ConcurrentDictionary<byte, byte[]> _inventoryItems = new();
        public byte InventoryExpansionState { get; set; } = 0;
        public uint InventoryZen { get; set; } = 0;
        public event Action MoneyChanged;
        private byte? _pendingSellSlot;
        private byte? _pendingVaultMoveFrom;
        private byte? _pendingVaultMoveTo;
        private byte? _pendingChaosMachineMoveFrom;
        private byte? _pendingChaosMachineMoveTo;

        // NPC Shop items
        private readonly ConcurrentDictionary<byte, byte[]> _shopItems = new();
        public event Action ShopItemsChanged;
        private readonly ConcurrentDictionary<byte, byte[]> _vaultItems = new();
        private readonly ConcurrentDictionary<byte, byte[]> _chaosMachineItems = new();

        // Last interacted NPC (for repair mode detection)
        public ushort LastNpcNetworkId { get; set; } = 0;
        public ushort LastNpcTypeNumber { get; set; } = 0;
        public event Action VaultItemsChanged;
        public event Action ChaosMachineItemsChanged;

        // Legacy quest state (A0/A1/A2/A3/A4) - used for class change quests (Sebina/Marlon/Devin)
        private readonly object _legacyQuestStateLock = new();
        private readonly byte[] _legacyQuestStateBytes = new byte[2];
        public bool HasLegacyQuestStateList { get; private set; }
        public event Action LegacyQuestStateChanged;

        public LegacyQuestState GetLegacyQuestState(byte questIndex)
        {
            lock (_legacyQuestStateLock)
            {
                int group = questIndex / 4;
                int offset = (questIndex % 4) * 2;
                if (group < 0 || group >= _legacyQuestStateBytes.Length)
                {
                    return LegacyQuestState.Undefined;
                }

                return (LegacyQuestState)((_legacyQuestStateBytes[group] >> offset) & 0x03);
            }
        }

        public void SetLegacyQuestStates(LegacyQuestStateList list)
        {
            if (list.Equals(default(LegacyQuestStateList)))
            {
                return;
            }

            lock (_legacyQuestStateLock)
            {
                _legacyQuestStateBytes[0] = PackLegacyQuestGroup(
                    list.ScrollOfEmperorState,
                    list.ThreeTreasuresOfMuState,
                    list.GainHeroStatusState,
                    list.SecretOfDarkStoneState);

                _legacyQuestStateBytes[1] = PackLegacyQuestGroup(
                    list.CertificateOfStrengthState,
                    list.InfiltrationOfBarrackState,
                    list.InfiltrationOfRefugeState,
                    list.UnusedQuestState);

                HasLegacyQuestStateList = true;
            }

            LegacyQuestStateChanged?.Invoke();
        }

        public void UpdateLegacyQuestStateGroup(byte questIndex, byte packedStates)
        {
            lock (_legacyQuestStateLock)
            {
                int group = questIndex / 4;
                if (group < 0 || group >= _legacyQuestStateBytes.Length)
                {
                    return;
                }

                _legacyQuestStateBytes[group] = packedStates;
                HasLegacyQuestStateList = true;
            }

            LegacyQuestStateChanged?.Invoke();
        }

        private static byte PackLegacyQuestGroup(LegacyQuestState q0, LegacyQuestState q1, LegacyQuestState q2, LegacyQuestState q3)
        {
            return (byte)(
                ((byte)q0 & 0x03) |
                (((byte)q1 & 0x03) << 2) |
                (((byte)q2 & 0x03) << 4) |
                (((byte)q3 & 0x03) << 6));
        }

        // Trade State
        private bool _isTradeActive;
        private ushort _tradePartnerId;
        private string _tradePartnerName = string.Empty;
        private ushort _tradePartnerLevel;
        private string _tradePartnerGuild = string.Empty;
        private readonly ConcurrentDictionary<byte, byte[]> _tradePartnerItems = new();
        private uint _tradePartnerMoney;
        private readonly ConcurrentDictionary<byte, byte[]> _myTradeItems = new();
        private uint _myTradeMoney;
        private MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState _myButtonState;
        private MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState _partnerButtonState;

        public event Action TradeWindowOpened;
        public event Action<TradeFinished.TradeResult> TradeFinished;
        public event Action TradeItemsChanged;
        public event Action TradeMoneyChanged;
        public event Action TradeButtonStateChanged;

        // Duel State
        public enum DuelPlayerType
        {
            Hero = 0,
            Enemy = 1,
        }

        private sealed class DuelPlayerInfo
        {
            public ushort Id;
            public string Name = string.Empty;
            public int Score;
            public float HpRate;
            public float SdRate;
        }

        private sealed class DuelChannelInfo
        {
            public bool Enabled;
            public bool Joinable;
            public string Player1Name = string.Empty;
            public string Player2Name = string.Empty;
        }

        private readonly object _duelLock = new();
        private bool _isDuelActive;
        private bool _isPetDuelActive;
        private readonly DuelPlayerInfo[] _duelPlayers = new DuelPlayerInfo[] { new DuelPlayerInfo(), new DuelPlayerInfo() };
        private readonly DuelChannelInfo[] _duelChannels = new DuelChannelInfo[] { new DuelChannelInfo(), new DuelChannelInfo(), new DuelChannelInfo(), new DuelChannelInfo() };
        private int _currentDuelChannel = -1;
        private bool _duelFightersRegenerated;
        private readonly List<string> _duelWatchUserList = new();

        public event Action DuelStateChanged;
        public event Action DuelChannelsChanged;
        public event Action DuelWatchUsersChanged;

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

        // Active Buffs/Effects
        private readonly ConcurrentDictionary<(byte EffectId, ushort PlayerIdMasked), ActiveBuffState> _activeBuffs = new();
        public event Action ActiveBuffsChanged;

        // --- Update Methods ---

        public uint CurrentHp => CurrentHealth;
        public uint MaxHp => MaximumHealth;
        public uint CurrentMp => CurrentMana;
        public uint MaxMp => MaximumMana;

        public event Action<uint, uint> HealthChanged; // (current, max)
        public event Action<uint, uint> ManaChanged;
        public event Action AttackSpeedsChanged;

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

        public event Action InventoryChanged;
        public event Action EquipmentChanged; // Raised only when slots 0-11 change

        private byte[] _pendingPickedItem;
        private byte? _pendingMoveFromSlot;
        private byte? _pendingMoveToSlot;

        /// <summary>
        /// Gets the raw ID of the item currently being picked up, if any.
        /// </summary>
        public ushort? PendingPickupRawId { get; private set; }

        /// <summary>
        /// Gets the inventory slot that has a pending move (item being transferred somewhere).
        /// Used to hide items that are being moved to vault/trade.
        /// </summary>
        public byte? PendingMoveFromSlot => _pendingMoveFromSlot;

        /// <summary>
        /// Sets the raw ID of the item currently being picked up.
        /// </summary>
        public void SetPendingPickupRawId(ushort rawId) => PendingPickupRawId = rawId;

        /// <summary>
        /// Clears the stored raw ID of the item pickup attempt.
        /// </summary>
        public void ClearPendingPickupRawId() => PendingPickupRawId = null;

        /// <summary>
        /// Stores item data temporarily before a pickup attempt.
        /// </summary>
        public void StashPickedItem(byte[] rawItemData)
        {
            _pendingPickedItem = rawItemData;
            _logger.LogTrace("Stashed item data for pending pickup: {ItemDataHex}", BitConverter.ToString(rawItemData ?? Array.Empty<byte>()).Replace("-", ""));
        }

        /// <summary>
        /// Commits the stashed item to the specified inventory slot.
        /// Clears the stash on success.
        /// </summary>
        public bool CommitStashedItem(byte slot)
        {
            if (_pendingPickedItem == null)
            {
                _logger.LogWarning("CommitStashedItem called for slot {Slot}, but no item was stashed.", slot);
                return false;
            }

            AddOrUpdateInventoryItem(slot, _pendingPickedItem);
            string itemName = ItemDatabase.GetItemName(_pendingPickedItem) ?? "Unknown Item";
            _logger.LogDebug("Committed stashed item '{ItemName}' to slot {Slot}.", itemName, slot);
            _pendingPickedItem = null; // Clear after successful commit
            return true;
        }

        /// <summary>
        /// Clears any stashed item data. Call this if a pickup attempt definitively fails.
        /// </summary>
        public void ClearPendingPickedItem()
        {
            if (_pendingPickedItem != null)
            {
                _logger.LogTrace("Cleared stashed pending picked item.");
                _pendingPickedItem = null;
            }
        }

        /// <summary>
        /// Stashes the last requested inventory move (fromSlot -> toSlot) until the server confirms or rejects it.
        /// </summary>
        public void StashPendingInventoryMove(byte fromSlot, byte toSlot)
        {
            _pendingMoveFromSlot = fromSlot;
            _pendingMoveToSlot = toSlot;
            _logger.LogTrace("Stashed pending inventory move: {From} -> {To}", fromSlot, toSlot);
        }

        /// <summary>
        /// Tries to consume the stashed inventory move. Returns true if there was one.
        /// </summary>
        public bool TryConsumePendingInventoryMove(out byte fromSlot, out byte toSlot)
        {
            if (_pendingMoveFromSlot.HasValue && _pendingMoveToSlot.HasValue)
            {
                fromSlot = _pendingMoveFromSlot.Value;
                toSlot = _pendingMoveToSlot.Value;
                _pendingMoveFromSlot = null;
                _pendingMoveToSlot = null;
                _logger.LogTrace("Consumed pending inventory move: {From} -> {To}", fromSlot, toSlot);
                return true;
            }

            fromSlot = 0;
            toSlot = 0;
            return false;
        }

        /// <summary>
        /// Returns true if a pending inventory move matches the provided slots.
        /// </summary>
        public bool IsInventoryMovePending(byte fromSlot, byte toSlot)
            => _pendingMoveFromSlot.HasValue && _pendingMoveToSlot.HasValue &&
               _pendingMoveFromSlot.Value == fromSlot && _pendingMoveToSlot.Value == toSlot;

        /// <summary>
        /// Clears any pending inventory move state. Useful when reopening UI to remove stale hides.
        /// </summary>
        public void ClearPendingInventoryMove()
        {
            if (_pendingMoveFromSlot.HasValue || _pendingMoveToSlot.HasValue)
            {
                _pendingMoveFromSlot = null;
                _pendingMoveToSlot = null;
                _logger.LogTrace("Cleared pending inventory move state.");
            }
        }

        /// <summary>
        /// Triggers the InventoryChanged event without modifying items.
        /// Useful to force UI refresh on client-side optimistic UI fallback.
        /// </summary>
        public void RaiseInventoryChanged()
        {
            InventoryChanged?.Invoke();
            _logger.LogTrace("InventoryChanged event raised explicitly.");
        }

        /// <summary>
        /// Raises EquipmentChanged event.
        /// </summary>
        public void RaiseEquipmentChanged()
        {
            EquipmentChanged?.Invoke();
            _logger.LogTrace("EquipmentChanged event raised.");
        }

        /// <summary>
        /// Stashes the inventory slot of an item for which a sell request was sent, until the server responds.
        /// </summary>
        public void StashPendingSellSlot(byte slot)
        {
            _pendingSellSlot = slot;
            _logger.LogTrace("Stashed pending sell for slot {Slot}", slot);
        }

        /// <summary>
        /// Tries to consume the stashed sell slot; returns true if one was available.
        /// </summary>
        public bool TryConsumePendingSellSlot(out byte slot)
        {
            if (_pendingSellSlot.HasValue)
            {
                slot = _pendingSellSlot.Value;
                _pendingSellSlot = null;
                _logger.LogTrace("Consumed pending sell for slot {Slot}", slot);
                return true;
            }
            slot = 0;
            return false;
        }

        /// <summary>
        /// Stashes a pending vault move (from->to). 'to' may be 0xFF when moving out to inventory.
        /// </summary>
        public void StashPendingVaultMove(byte fromSlot, byte toSlot)
        {
            _pendingVaultMoveFrom = fromSlot;
            _pendingVaultMoveTo = toSlot;
            _logger.LogTrace("Stashed pending vault move: {From} -> {To}", fromSlot, toSlot);
        }

        public bool TryConsumePendingVaultMove(out byte fromSlot, out byte toSlot)
        {
            if (_pendingVaultMoveFrom.HasValue)
            {
                fromSlot = _pendingVaultMoveFrom.Value;
                toSlot = _pendingVaultMoveTo ?? (byte)0xFF;
                _pendingVaultMoveFrom = null;
                _pendingVaultMoveTo = null;
                _logger.LogTrace("Consumed pending vault move: {From} -> {To}", fromSlot, toSlot);
                return true;
            }
            fromSlot = 0; toSlot = 0xFF;
            return false;
        }

        /// <summary>
        /// Returns true if a pending vault move matches the provided slots.
        /// </summary>
        public bool IsVaultMovePending(byte fromSlot, byte toSlot)
            => _pendingVaultMoveFrom.HasValue &&
               _pendingVaultMoveFrom.Value == fromSlot &&
               ((_pendingVaultMoveTo.HasValue ? _pendingVaultMoveTo.Value : (byte)0xFF) == toSlot);

        /// <summary>
        /// Stashes a pending chaos machine move (from-&gt;to). 'to' may be 0xFF when moving out to inventory.
        /// </summary>
        public void StashPendingChaosMachineMove(byte fromSlot, byte toSlot)
        {
            _pendingChaosMachineMoveFrom = fromSlot;
            _pendingChaosMachineMoveTo = toSlot;
            _logger.LogTrace("Stashed pending chaos machine move: {From} -> {To}", fromSlot, toSlot);
        }

        public bool TryConsumePendingChaosMachineMove(out byte fromSlot, out byte toSlot)
        {
            if (_pendingChaosMachineMoveFrom.HasValue)
            {
                fromSlot = _pendingChaosMachineMoveFrom.Value;
                toSlot = _pendingChaosMachineMoveTo ?? (byte)0xFF;
                _pendingChaosMachineMoveFrom = null;
                _pendingChaosMachineMoveTo = null;
                _logger.LogTrace("Consumed pending chaos machine move: {From} -> {To}", fromSlot, toSlot);
                return true;
            }

            fromSlot = 0;
            toSlot = 0xFF;
            return false;
        }

        public bool IsChaosMachineMovePending(byte fromSlot, byte toSlot)
            => _pendingChaosMachineMoveFrom.HasValue &&
               _pendingChaosMachineMoveFrom.Value == fromSlot &&
               ((_pendingChaosMachineMoveTo.HasValue ? _pendingChaosMachineMoveTo.Value : (byte)0xFF) == toSlot);

        /// <summary>
        /// Clears the current NPC shop items and notifies listeners.
        /// </summary>
        public void ClearShopItems()
        {
            _shopItems.Clear();
            ShopItemsChanged?.Invoke();
            _logger.LogTrace("ShopItemsChanged raised after ClearShopItems.");
        }

        /// <summary>
        /// Adds or updates an item in the NPC shop list (by local shop slot).
        /// </summary>
        public void AddOrUpdateShopItem(byte slot, byte[] itemData)
        {
            _shopItems[slot] = itemData;
        }

        /// <summary>
        /// Notifies that shop items were updated. Call after bulk updates.
        /// </summary>
        public void RaiseShopItemsChanged()
        {
            ShopItemsChanged?.Invoke();
            _logger.LogTrace("ShopItemsChanged event raised.");
        }

        /// <summary>
        /// Snapshot of current NPC shop items.
        /// </summary>
        public IReadOnlyDictionary<byte, byte[]> GetShopItems()
        {
            return new ReadOnlyDictionary<byte, byte[]>(_shopItems.ToDictionary(k => k.Key, v => v.Value));
        }

        /// <summary>
        /// Vault items API.
        /// </summary>
        public void ClearVaultItems()
        {
            _vaultItems.Clear();
            VaultItemsChanged?.Invoke();
            _logger.LogTrace("VaultItemsChanged raised after ClearVaultItems.");
        }

        public void AddOrUpdateVaultItem(byte slot, byte[] itemData)
        {
            _vaultItems[slot] = itemData;
        }

        public void RemoveVaultItem(byte slot)
        {
            _vaultItems.TryRemove(slot, out _);
        }

        public void RaiseVaultItemsChanged()
        {
            VaultItemsChanged?.Invoke();
            _logger.LogTrace("VaultItemsChanged event raised.");
        }

        public IReadOnlyDictionary<byte, byte[]> GetVaultItems()
        {
            return new ReadOnlyDictionary<byte, byte[]>(_vaultItems.ToDictionary(k => k.Key, v => v.Value));
        }

        /// <summary>
        /// Chaos machine items API.
        /// </summary>
        public void ClearChaosMachineItems()
        {
            _chaosMachineItems.Clear();
            ChaosMachineItemsChanged?.Invoke();
            _logger.LogTrace("ChaosMachineItemsChanged raised after ClearChaosMachineItems.");
        }

        public void AddOrUpdateChaosMachineItem(byte slot, byte[] itemData)
        {
            _chaosMachineItems[slot] = itemData;
        }

        public void RemoveChaosMachineItem(byte slot)
        {
            _chaosMachineItems.TryRemove(slot, out _);
        }

        public void RaiseChaosMachineItemsChanged()
        {
            ChaosMachineItemsChanged?.Invoke();
            _logger.LogTrace("ChaosMachineItemsChanged event raised.");
        }

        public IReadOnlyDictionary<byte, byte[]> GetChaosMachineItems()
        {
            return new ReadOnlyDictionary<byte, byte[]>(_chaosMachineItems.ToDictionary(k => k.Key, v => v.Value));
        }

        /// <summary>
        /// Trade state API.
        /// </summary>
        public bool IsTradeActive => _isTradeActive;
        public ushort TradePartnerId => _tradePartnerId;
        public string TradePartnerName => _tradePartnerName;
        public ushort TradePartnerLevel => _tradePartnerLevel;
        public string TradePartnerGuild => _tradePartnerGuild;
        public uint TradePartnerMoney => _tradePartnerMoney;
        public uint MyTradeMoney => _myTradeMoney;
        public MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState MyTradeButtonState => _myButtonState;
        public MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState PartnerTradeButtonState => _partnerButtonState;

        public void StartTrade(ushort partnerId, string partnerName, ushort partnerLevel, string partnerGuild = "")
        {
            _isTradeActive = true;
            _tradePartnerId = partnerId;
            _tradePartnerName = partnerName ?? string.Empty;
            _tradePartnerLevel = partnerLevel;
            _tradePartnerGuild = partnerGuild ?? string.Empty;
            _tradePartnerItems.Clear();
            _myTradeItems.Clear();
            _tradePartnerMoney = 0;
            _myTradeMoney = 0;
            _myButtonState = MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState.Unchecked;
            _partnerButtonState = MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState.Unchecked;
            _logger.LogInformation("Trade started with {Partner} (Level {Level})", partnerName, partnerLevel);
            TradeWindowOpened?.Invoke();
        }

        public void EndTrade(TradeFinished.TradeResult result)
        {
            _isTradeActive = false;
            _tradePartnerId = 0;
            _tradePartnerName = string.Empty;
            _tradePartnerLevel = 0;
            _tradePartnerGuild = string.Empty;
            _tradePartnerItems.Clear();
            _myTradeItems.Clear();
            _tradePartnerMoney = 0;
            _myTradeMoney = 0;
            _myButtonState = MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState.Unchecked;
            _partnerButtonState = MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState.Unchecked;
            _logger.LogInformation("Trade ended with result: {Result}", result);
            TradeFinished?.Invoke(result);
        }

        public void AddOrUpdatePartnerTradeItem(byte slot, byte[] itemData)
        {
            _tradePartnerItems[slot] = itemData;
            TradeItemsChanged?.Invoke();
        }

        public void RemovePartnerTradeItem(byte slot)
        {
            _tradePartnerItems.TryRemove(slot, out _);
            TradeItemsChanged?.Invoke();
        }

        public void AddOrUpdateMyTradeItem(byte slot, byte[] itemData)
        {
            _myTradeItems[slot] = itemData;
            TradeItemsChanged?.Invoke();
        }

        public void RemoveMyTradeItem(byte slot)
        {
            _myTradeItems.TryRemove(slot, out _);
            TradeItemsChanged?.Invoke();
        }

        public void SetPartnerTradeMoney(uint amount)
        {
            _tradePartnerMoney = amount;
            TradeMoneyChanged?.Invoke();
        }

        public void SetMyTradeMoney(uint amount)
        {
            _myTradeMoney = amount;
            TradeMoneyChanged?.Invoke();
        }

        public void SetMyTradeButtonState(MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState state)
        {
            _myButtonState = state;
            TradeButtonStateChanged?.Invoke();
        }

        public void SetPartnerTradeButtonState(MUnique.OpenMU.Network.Packets.ServerToClient.TradeButtonStateChanged.TradeButtonState state)
        {
            _partnerButtonState = state;
            TradeButtonStateChanged?.Invoke();
        }

        public IReadOnlyDictionary<byte, byte[]> GetPartnerTradeItems()
        {
            return new ReadOnlyDictionary<byte, byte[]>(_tradePartnerItems.ToDictionary(k => k.Key, v => v.Value));
        }

        public IReadOnlyDictionary<byte, byte[]> GetMyTradeItems()
        {
            return new ReadOnlyDictionary<byte, byte[]>(_myTradeItems.ToDictionary(k => k.Key, v => v.Value));
        }

        /// <summary>
        /// Duel state API (mirrors SourceMain5.2 DuelMgr).
        /// </summary>
        public bool IsDuelActive
        {
            get
            {
                lock (_duelLock)
                {
                    return _isDuelActive;
                }
            }
        }

        public bool IsPetDuelActive
        {
            get
            {
                lock (_duelLock)
                {
                    return _isPetDuelActive;
                }
            }
        }

        public int CurrentDuelChannel
        {
            get
            {
                lock (_duelLock)
                {
                    return _currentDuelChannel;
                }
            }
        }

        public void EnableDuel(bool enabled)
        {
            bool changed;
            lock (_duelLock)
            {
                changed = _isDuelActive != enabled;
                _isDuelActive = enabled;
                if (!enabled)
                {
                    ResetDuel_NoEvent();
                }
                else
                {
                    // Start with full bars until server sends the first updates.
                    _duelPlayers[(int)DuelPlayerType.Hero].HpRate = 1f;
                    _duelPlayers[(int)DuelPlayerType.Hero].SdRate = 1f;
                    _duelPlayers[(int)DuelPlayerType.Enemy].HpRate = 1f;
                    _duelPlayers[(int)DuelPlayerType.Enemy].SdRate = 1f;
                }
            }

            if (changed || !enabled)
            {
                DuelStateChanged?.Invoke();
                DuelChannelsChanged?.Invoke();
                DuelWatchUsersChanged?.Invoke();
            }
        }

        public void EnablePetDuel(bool enabled)
        {
            bool changed;
            lock (_duelLock)
            {
                changed = _isPetDuelActive != enabled;
                _isPetDuelActive = enabled;
            }

            if (changed)
            {
                DuelStateChanged?.Invoke();
            }
        }

        public void SetHeroAsDuelPlayer(DuelPlayerType playerType)
        {
            SetDuelPlayer(playerType, Id, Name);
        }

        public void SetDuelPlayer(DuelPlayerType playerType, ushort id, string name)
        {
            lock (_duelLock)
            {
                var info = _duelPlayers[(int)playerType];
                info.Id = NormalizeDuelObjectId(id);
                info.Name = name ?? string.Empty;
            }
            DuelStateChanged?.Invoke();
        }

        public void SetDuelScore(DuelPlayerType playerType, int score)
        {
            lock (_duelLock)
            {
                _duelPlayers[(int)playerType].Score = score;
            }
            DuelStateChanged?.Invoke();
        }

        public void SetDuelHp(DuelPlayerType playerType, int percentage)
        {
            lock (_duelLock)
            {
                _duelPlayers[(int)playerType].HpRate = percentage * 0.01f;
            }
            DuelStateChanged?.Invoke();
        }

        public void SetDuelSd(DuelPlayerType playerType, int percentage)
        {
            lock (_duelLock)
            {
                _duelPlayers[(int)playerType].SdRate = percentage * 0.01f;
            }
            DuelStateChanged?.Invoke();
        }

        public ushort GetDuelPlayerId(DuelPlayerType playerType)
        {
            lock (_duelLock)
            {
                return _duelPlayers[(int)playerType].Id;
            }
        }

        public string GetDuelPlayerName(DuelPlayerType playerType)
        {
            lock (_duelLock)
            {
                return _duelPlayers[(int)playerType].Name;
            }
        }

        public int GetDuelScore(DuelPlayerType playerType)
        {
            lock (_duelLock)
            {
                return _duelPlayers[(int)playerType].Score;
            }
        }

        public float GetDuelHpRate(DuelPlayerType playerType)
        {
            lock (_duelLock)
            {
                return _duelPlayers[(int)playerType].HpRate;
            }
        }

        public float GetDuelSdRate(DuelPlayerType playerType)
        {
            lock (_duelLock)
            {
                return _duelPlayers[(int)playerType].SdRate;
            }
        }

        public bool IsDuelPlayer(ushort id, DuelPlayerType playerType)
        {
            lock (_duelLock)
            {
                return _duelPlayers[(int)playerType].Id == NormalizeDuelObjectId(id);
            }
        }

        public void SetDuelChannel(int channelIndex, bool enabled, bool joinable, string player1Name, string player2Name)
        {
            if (channelIndex < 0 || channelIndex >= _duelChannels.Length)
            {
                return;
            }

            lock (_duelLock)
            {
                var channel = _duelChannels[channelIndex];
                channel.Enabled = enabled;
                channel.Joinable = joinable;
                channel.Player1Name = player1Name ?? string.Empty;
                channel.Player2Name = player2Name ?? string.Empty;
            }

            DuelChannelsChanged?.Invoke();
        }

        public void SetCurrentDuelChannel(int channelIndex)
        {
            lock (_duelLock)
            {
                _currentDuelChannel = channelIndex;
            }
            DuelChannelsChanged?.Invoke();
        }

        public void RemoveAllDuelWatchUsers()
        {
            lock (_duelLock)
            {
                _duelWatchUserList.Clear();
            }
            DuelWatchUsersChanged?.Invoke();
        }

        public void AddDuelWatchUser(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            lock (_duelLock)
            {
                if (!_duelWatchUserList.Contains(name))
                {
                    _duelWatchUserList.Add(name);
                }
            }
            DuelWatchUsersChanged?.Invoke();
        }

        public void RemoveDuelWatchUser(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            lock (_duelLock)
            {
                _duelWatchUserList.Remove(name);
            }
            DuelWatchUsersChanged?.Invoke();
        }

        public IReadOnlyList<string> GetDuelWatchUsers()
        {
            lock (_duelLock)
            {
                return _duelWatchUserList.ToArray();
            }
        }

        public void SetDuelFightersRegenerated(bool regenerated)
        {
            lock (_duelLock)
            {
                _duelFightersRegenerated = regenerated;
            }
            DuelStateChanged?.Invoke();
        }

        private void ResetDuel_NoEvent()
        {
            _isDuelActive = false;
            _isPetDuelActive = false;
            _currentDuelChannel = -1;
            _duelFightersRegenerated = false;

            for (int i = 0; i < _duelPlayers.Length; i++)
            {
                _duelPlayers[i].Id = 0;
                _duelPlayers[i].Name = string.Empty;
                _duelPlayers[i].Score = 0;
                _duelPlayers[i].HpRate = 0;
                _duelPlayers[i].SdRate = 0;
            }

            for (int i = 0; i < _duelChannels.Length; i++)
            {
                _duelChannels[i].Enabled = false;
                _duelChannels[i].Joinable = false;
                _duelChannels[i].Player1Name = string.Empty;
                _duelChannels[i].Player2Name = string.Empty;
            }

            _duelWatchUserList.Clear();
        }

        private static ushort NormalizeDuelObjectId(ushort id) => (ushort)(id & 0x7FFF);

        /// <summary>
        /// Checks if an item exists at the given slot in the current state.
        /// </summary>
        public bool HasInventoryItem(byte slot) => _inventoryItems.ContainsKey(slot);

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
        /// Updates attack/magic speeds (server-provided in Season 6).
        /// If the server sends 0, falls back to stat-based calculation.
        /// </summary>
        public void UpdateAttackSpeeds(ushort attackSpeed, ushort magicSpeed, ushort? maximumAttackSpeed = null)
        {
            // Fallback to calculated values when server sends 0
            if (attackSpeed == 0)
            {
                attackSpeed = CalculateAttackSpeed();
                _logger.LogDebug("Server sent AttackSpeed=0, calculated fallback: {AttackSpeed}", attackSpeed);
            }

            if (magicSpeed == 0)
            {
                magicSpeed = CalculateMagicSpeed();
                _logger.LogDebug("Server sent MagicSpeed=0, calculated fallback: {MagicSpeed}", magicSpeed);
            }

            bool changed = AttackSpeed != attackSpeed || MagicSpeed != magicSpeed;

            AttackSpeed = attackSpeed;
            MagicSpeed = magicSpeed;
            if (maximumAttackSpeed.HasValue)
                MaximumAttackSpeed = maximumAttackSpeed.Value;

            _logger.LogDebug("Attack speeds updated. Attack={AttackSpeed}, Magic={MagicSpeed}, MaxAttack={MaxAttackSpeed}",
                AttackSpeed, MagicSpeed, MaximumAttackSpeed);

            if (changed)
                AttackSpeedsChanged?.Invoke();
        }

        /// <summary>
        /// Calculates attack speed based on agility, character class, and equipped items.
        /// Used as fallback when server doesn't provide attack speed.
        /// </summary>
        private ushort CalculateAttackSpeed()
        {
            var agi = TotalAgility;

            // Base attack speed from agility and class
            int baseSpeed = Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    agi / 15,
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    agi / 10,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 50,
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    agi / 15,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 10,
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    agi / 20,
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    agi / 9,
                _ => 0
            };

            // Add equipment bonuses (weapon speed, excellent options, Wizard's Ring)
            int equipmentBonus = CalculateEquipmentAttackSpeedBonus();
            int totalSpeed = baseSpeed + equipmentBonus;

            return (ushort)Math.Max(0, totalSpeed);
        }

        /// <summary>
        /// Calculates magic speed based on agility and character class.
        /// Used as fallback when server doesn't provide magic speed.
        /// For most classes, magic speed equals attack speed.
        /// </summary>
        private ushort CalculateMagicSpeed()
        {
            // For wizard/summoner classes, magic speed might differ
            // For now, use the same formula as attack speed
            // This can be adjusted if different formulas are needed
            return CalculateAttackSpeed();
        }

        /// <summary>
        /// Calculates total attack speed bonus from equipped items.
        /// Includes: weapon attack speed (max from dual-wield), excellent speed options, and Wizard's Ring.
        /// </summary>
        public int CalculateEquipmentAttackSpeedBonus()
        {
            int totalBonus = 0;

            // Equipment slots: 0 = Right Hand, 1 = Left Hand, 10-11 = Rings, 9 = Pendant
            const byte SLOT_RIGHT_HAND = 0;
            const byte SLOT_LEFT_HAND = 1;
            const byte SLOT_RING_LEFT = 10;
            const byte SLOT_RING_RIGHT = 11;
            const byte SLOT_PENDANT = 9;

            const byte WIZARD_RING_GROUP = 13;
            const byte WIZARD_RING_ID = 20;

            const byte EXC_SPEED_BIT = 0b0000_1000;
            const int EXC_SPEED_BONUS = 7; // Excellent Speed option gives +7 attack speed

            // 1. Get max weapon attack speed (from right or left hand)
            int maxWeaponSpeed = 0;

            if (_inventoryItems.TryGetValue(SLOT_RIGHT_HAND, out byte[] rightHandData) && rightHandData.Length >= 6)
            {
                byte group = (byte)(rightHandData[5] >> 4);
                short id = rightHandData[0];
                var weaponDef = Core.Utilities.ItemDatabase.GetItemDefinition(group, id);
                if (weaponDef != null)
                {
                    maxWeaponSpeed = Math.Max(maxWeaponSpeed, weaponDef.AttackSpeed);
                }
            }

            if (_inventoryItems.TryGetValue(SLOT_LEFT_HAND, out byte[] leftHandData) && leftHandData.Length >= 6)
            {
                byte group = (byte)(leftHandData[5] >> 4);
                short id = leftHandData[0];
                var weaponDef = Core.Utilities.ItemDatabase.GetItemDefinition(group, id);
                if (weaponDef != null)
                {
                    maxWeaponSpeed = Math.Max(maxWeaponSpeed, weaponDef.AttackSpeed);
                }
            }

            totalBonus += maxWeaponSpeed;

            // 2. Check all equipment slots for Excellent Speed option
            foreach (var kvp in _inventoryItems)
            {
                byte slot = kvp.Key;
                byte[] itemData = kvp.Value;

                // Only check equipment slots (0-11)
                if (slot > 11 || itemData.Length < 4)
                    continue;

                byte excByte = itemData[3];
                if ((excByte & EXC_SPEED_BIT) != 0)
                {
                    totalBonus += EXC_SPEED_BONUS;
                }
            }

            // 3. Check for Wizard's Ring (+10 attack speed)
            if (_inventoryItems.TryGetValue(SLOT_RING_LEFT, out byte[] leftRingData) && leftRingData.Length >= 6)
            {
                byte group = (byte)(leftRingData[5] >> 4);
                short id = leftRingData[0];
                if (group == WIZARD_RING_GROUP && id == WIZARD_RING_ID)
                {
                    totalBonus += 10;
                }
            }

            if (_inventoryItems.TryGetValue(SLOT_RING_RIGHT, out byte[] rightRingData) && rightRingData.Length >= 6)
            {
                byte group = (byte)(rightRingData[5] >> 4);
                short id = rightRingData[0];
                if (group == WIZARD_RING_GROUP && id == WIZARD_RING_ID)
                {
                    totalBonus += 10;
                }
            }

            // Also check pendant slot (some servers allow Wizard's Ring in pendant slot)
            if (_inventoryItems.TryGetValue(SLOT_PENDANT, out byte[] pendantData) && pendantData.Length >= 6)
            {
                byte group = (byte)(pendantData[5] >> 4);
                short id = pendantData[0];
                if (group == WIZARD_RING_GROUP && id == WIZARD_RING_ID)
                {
                    totalBonus += 10;
                }
            }

            return totalBonus;
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
            MoneyChanged?.Invoke();
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
            InventoryChanged?.Invoke();
            _logger.LogDebug("Inventory cleared.");
        }

        /// <summary>
        /// Adds or updates an item in the character's inventory at a specific slot.
        /// </summary>
        public void AddOrUpdateInventoryItem(byte slot, byte[] itemData)
        {
            _inventoryItems[slot] = itemData;
            InventoryChanged?.Invoke();
            if (slot < 12) // equipment slots 0..11
            {
                EquipmentChanged?.Invoke();

                // Recalculate attack speeds if equipment changes affect attack speed
                // (weapons, rings, pendants with excellent speed options)
                if (IsAttackSpeedAffectingSlot(slot))
                {
                    RecalculateAttackSpeedsFromEquipment();
                }
            }
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
                InventoryChanged?.Invoke();
                if (slot < 12) // equipment slots 0..11
                {
                    EquipmentChanged?.Invoke();

                    // Recalculate attack speeds if equipment changes affect attack speed
                    if (IsAttackSpeedAffectingSlot(slot))
                    {
                        RecalculateAttackSpeedsFromEquipment();
                    }
                }
                _logger.LogDebug("Inventory item removed from slot {Slot}", slot);
            }
            else
            {
                _logger.LogWarning("Attempted to remove inventory item from slot {Slot}, but no item found.", slot);
            }
        }

        /// <summary>
        /// Checks if the given equipment slot can affect attack speed.
        /// This includes weapons (0-1), rings (10-11), and pendant (9).
        /// Any equipment slot can also have excellent speed options.
        /// </summary>
        private bool IsAttackSpeedAffectingSlot(byte slot)
        {
            // Weapons directly provide attack speed
            if (slot == 0 || slot == 1) return true;

            // Rings can be Wizard's Ring (+10 speed)
            if (slot == 10 || slot == 11) return true;

            // Pendant can also be Wizard's Ring
            if (slot == 9) return true;

            // Any equipment slot can have excellent speed option
            // So we check all equipment slots (0-11)
            return slot < 12;
        }

        /// <summary>
        /// Recalculates attack speeds from current equipment.
        /// Used when equipment changes and server hasn't sent updated speeds.
        /// </summary>
        private void RecalculateAttackSpeedsFromEquipment()
        {
            // Recalculate using current equipment
            ushort newAttackSpeed = CalculateAttackSpeed();
            ushort newMagicSpeed = CalculateMagicSpeed();

            // Only fire event if values actually changed
            bool changed = AttackSpeed != newAttackSpeed || MagicSpeed != newMagicSpeed;

            AttackSpeed = newAttackSpeed;
            MagicSpeed = newMagicSpeed;

            if (changed)
            {
                _logger.LogDebug("Attack speeds recalculated from equipment. Attack={AttackSpeed}, Magic={MagicSpeed}",
                    AttackSpeed, MagicSpeed);
                AttackSpeedsChanged?.Invoke();
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
                    RaiseInventoryChanged(); // Trigger UI refresh after durability update
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

        // --- Active Buffs/Effects Methods ---

        /// <summary>
        /// Adds or activates a buff/effect.
        /// </summary>
        public void ActivateBuff(byte effectId, ushort playerId)
        {
            ushort maskedId = (ushort)(playerId & 0x7FFF);
            var key = (effectId, maskedId);
            _activeBuffs[key] = new ActiveBuffState
            {
                EffectId = effectId,
                PlayerIdRaw = playerId,
                PlayerIdMasked = maskedId,
                ActivatedAt = DateTime.UtcNow
            };
            ActiveBuffsChanged?.Invoke();
            _logger.LogDebug("Buff activated. Effect ID: {EffectId}, Player ID: {PlayerId} (masked {MaskedId})", effectId, playerId, maskedId);
        }

        /// <summary>
        /// Removes/deactivates a buff/effect.
        /// </summary>
        public void DeactivateBuff(byte effectId, ushort playerId)
        {
            ushort maskedId = (ushort)(playerId & 0x7FFF);
            var key = (effectId, maskedId);
            bool removed = _activeBuffs.TryRemove(key, out _);
            if (removed)
            {
                ActiveBuffsChanged?.Invoke();
                _logger.LogDebug("Buff deactivated. Effect ID: {EffectId}, Player ID: {PlayerId} (masked {MaskedId})", effectId, playerId, maskedId);
            }
            else
            {
                _logger.LogWarning("Attempted to deactivate buff with ID {EffectId} for player {PlayerId} (masked {MaskedId}), but buff not found.", effectId, playerId, maskedId);
            }
        }

        /// <summary>
        /// Gets all active buffs.
        /// </summary>
        public IEnumerable<ActiveBuffState> GetActiveBuffs()
        {
            ushort selfMasked = (ushort)(Id & 0x7FFF);
            return _activeBuffs.Values
                .Where(b => b.PlayerIdMasked == selfMasked)
                .OrderBy(b => b.ActivatedAt);
        }

        public bool HasActiveBuff(byte effectId, ushort playerId)
        {
            ushort maskedId = (ushort)(playerId & 0x7FFF);
            return _activeBuffs.ContainsKey((effectId, maskedId));
        }

        /// <summary>
        /// Clears all active buffs.
        /// </summary>
        public void ClearActiveBuffs()
        {
            _activeBuffs.Clear();
            ActiveBuffsChanged?.Invoke();
            _logger.LogDebug("All active buffs cleared.");
        }

        // --- Display Methods ---

        /// <summary>
        /// Gets a formatted string representation of the character's inventory.
        /// </summary>
        public string GetInventoryDisplay()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n--- Inventory ---");
            sb.AppendLine($"  Zen: {InventoryZen}");
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

                    // Użycie nowej logiki do wyświetlania
                    var details = ItemDatabase.ParseItemDetails(itemData);
                    var sbDetails = new StringBuilder();
                    if (details.Level > 0) sbDetails.Append($" +{details.Level}");
                    if (details.HasSkill) sbDetails.Append(" +Skill");
                    if (details.HasLuck) sbDetails.Append(" +Luck");
                    if (details.OptionLevel > 0) sbDetails.Append($" +{details.OptionLevel * 4} Opt");
                    if (details.IsExcellent) sbDetails.Append(" +Exc");
                    if (details.IsAncient) sbDetails.Append(" +Anc");

                    sb.AppendLine($"  Slot {slot,3}: {itemName}{sbDetails}");
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
                stats.Add(new KeyValuePair<string, string>("Exp", $"{Experience} / {ExperienceForNextLevel}"));
                stats.Add(new KeyValuePair<string, string>("M.Exp", $"{MasterExperience} / {MasterExperienceForNextLevel}"));
            }
            else
            {
                stats.Add(new KeyValuePair<string, string>("Exp", $"{Experience} / {ExperienceForNextLevel}"));
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
            sb.AppendLine($"  Exp: {Experience} / {ExperienceForNextLevel}");
            if (MasterLevel > 0)
            {
                sb.AppendLine($"  M.Exp: {MasterExperience} / {MasterExperienceForNextLevel}");
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
