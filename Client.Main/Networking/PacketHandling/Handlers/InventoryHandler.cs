using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Client; // For CharacterState, SimpleLoginClient, TargetProtocolVersion
using Client.Main.Core.Utilities;
using System;
using System.Threading.Tasks; // For PacketHandlerAttribute, ItemDatabase

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to inventory, items, and money.
    /// </summary>
    public class InventoryHandler : IGamePacketHandler
    {
        private readonly ILogger<InventoryHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager; // Needed for pickup flags and movement handling
        private readonly TargetProtocolVersion _targetVersion;

        public InventoryHandler(ILoggerFactory loggerFactory, CharacterState characterState, NetworkManager networkManager, TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<InventoryHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        [PacketHandler(0xF3, 0x10)] // CharacterInventory
        public Task HandleCharacterInventoryAsync(Memory<byte> packet)
        {
            try
            {
                UpdateInventoryFromPacket(packet); // Call helper to update CharacterState
                // _networkManager.UpdateConsoleTitle(); // Update title after inventory changes
                // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error processing CharacterInventory packet.");
            }
            return Task.CompletedTask;
        }

        // Helper method to parse inventory based on version
        private void UpdateInventoryFromPacket(Memory<byte> inventoryPacketData)
        {
            _characterState.ClearInventory();
            int itemCount = 0;
            int firstItemOffset = 0;
            int itemDataSize = 0;

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var inventoryS6 = new CharacterInventoryRef(inventoryPacketData.Span);
                    itemCount = inventoryS6.ItemCount;
                    firstItemOffset = 6; itemDataSize = 12;
                    _logger.LogInformation("🎒 Updating inventory (S6) ({Count} items)...", itemCount);
                    break;
                case TargetProtocolVersion.Version097:
                    var header097 = new C1HeaderWithSubCodeRef(inventoryPacketData.Span);
                    if (header097.Code != 0xF3 || header097.SubCode != 0x10) { _logger.LogWarning("Received unexpected packet instead of 0.97 Inventory."); return; }
                    itemCount = inventoryPacketData.Span[4];
                    firstItemOffset = 5; itemDataSize = 11;
                    _logger.LogInformation("🎒 Updating inventory (0.97/0.95) ({Count} items)...", itemCount);
                    break;
                case TargetProtocolVersion.Version075:
                    var header075 = new C1HeaderWithSubCodeRef(inventoryPacketData.Span);
                    if (header075.Code != 0xF3 || header075.SubCode != 0x10) { _logger.LogWarning("Received unexpected packet instead of 0.75 Inventory."); return; }
                    itemCount = inventoryPacketData.Span[4];
                    firstItemOffset = 5; itemDataSize = 7;
                    _logger.LogInformation("🎒 Updating inventory (0.75) ({Count} items)...", itemCount);
                    break;
                default:
                    _logger.LogWarning("Inventory handling for version {Version} not implemented.", _targetVersion);
                    return;
            }

            int storedItemSize = 1 + itemDataSize;
            int currentOffset = firstItemOffset;
            for (int i = 0; i < itemCount; i++)
            {
                if (currentOffset + storedItemSize > inventoryPacketData.Length) { _logger.LogWarning("Inventory packet too short while parsing item {ItemIndex}", i); break; }
                byte itemSlot = inventoryPacketData.Span[currentOffset];
                var itemData = inventoryPacketData.Slice(currentOffset + 1, itemDataSize);
                _characterState.AddOrUpdateInventoryItem(itemSlot, itemData.ToArray());
                _logger.LogDebug($"  -> Slot {itemSlot}: {ItemDatabase.GetItemName(itemData.Span) ?? "Unknown Item"} (DataLen: {itemDataSize})");
                currentOffset += storedItemSize;
            }

            // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
            // _networkManager.ViewModel.UpdateCharacterStateDisplay(); // Zaktualizuj też tytuł okna i inne info zależne od stanu
        }

        [PacketHandler(0x22, 0xFE)] // InventoryMoneyUpdate
        public Task HandleInventoryMoneyUpdateAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < InventoryMoneyUpdate.Length) { _logger.LogWarning("⚠️ Received InventoryMoneyUpdate packet (0x22, FE) with unexpected length {Length}.", packet.Length); return Task.CompletedTask; }
                var moneyUpdate = new InventoryMoneyUpdate(packet);
                _logger.LogInformation("💰 Inventory Money Updated: {Amount} Zen.", moneyUpdate.Money);
                // _networkManager.UpdateConsoleTitle();
                _characterState.UpdateInventoryZen(moneyUpdate.Money);
                // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
                // _networkManager.SignalMovementHandledIfWalking();
                // _networkManager.ViewModel.UpdateCharacterStateDisplay(); // Zaktualizuj tytuł itp.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing InventoryMoneyUpdate (22, FE). Packet: {PacketData}", Convert.ToHexString(packet.Span));
                // _networkManager.SignalMovementHandledIfWalking();
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x22, 0x01)] // ItemAddedToInventory (Success case for pickup)
        public Task HandleItemAddedToInventoryAsync(Memory<byte> packet)
        {
            byte headerType = packet.Span[0];
            try
            {
                int minLength = (headerType == 0xC1 || headerType == 0xC3) ? 4 : 5;
                if (packet.Length < minLength + 1) {
                    _logger.LogWarning("⚠️ Received ItemAddedToInventory packet (0x22) too short. Length: {Length}. Packet: {PacketData}", packet.Length, Convert.ToHexString(packet.Span));
                    // _networkManager.LastPickupSucceeded = false;
                    // _networkManager.PickupHandled = true;
                    // _networkManager.SignalMovementHandledIfWalking();
                    return Task.CompletedTask; }

                var itemAdded = new ItemAddedToInventory(packet);
                var itemData = itemAdded.ItemData;
                // _networkManager.PickupHandled = true;
                if (itemData.Length < 1) {
                    _logger.LogWarning("⚠️ ItemData in ItemAddedToInventory is empty. Raw packet: {PacketData}", Convert.ToHexString(packet.Span));
                    // _networkManager.LastPickupSucceeded = false;
                    // _networkManager.SignalMovementHandledIfWalking();
                    return Task.CompletedTask; }

                // _networkManager.LastPickupSucceeded = true;
                _characterState.AddOrUpdateInventoryItem(itemAdded.InventorySlot, itemData.ToArray());

                string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                _logger.LogInformation("✅ Picked up '{ItemName}' into inventory slot {Slot}.", itemName, itemAdded.InventorySlot);

                // _networkManager.SignalMovementHandledIfWalking();
                // _networkManager.UpdateConsoleTitle();
                // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
                // _networkManager.ViewModel.UpdateCharacterStateDisplay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error processing ItemAddedToInventory (0x22). Packet Type: {HeaderType:X2}, Data: {PacketData}", headerType, Convert.ToHexString(packet.Span));
                // _networkManager.LastPickupSucceeded = false; _networkManager.PickupHandled = true; _networkManager.SignalMovementHandledIfWalking();
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x22, 0xFF)] // ItemPickupFailed (Explicit failure case)
        public Task HandleItemPickupFailedSubCodeAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ItemPickUpRequestFailed.Length) { _logger.LogWarning("⚠️ Received ItemPickupFailed packet (0x22, FF) with unexpected length {Length}.", packet.Length); return Task.CompletedTask; }
                var response = new ItemPickUpRequestFailed(packet);
                // More descriptive player-facing reason
                string reasonText = response.FailReason switch
                {
                    ItemPickUpRequestFailed.ItemPickUpFailReason.General => "Failed to pick up item.",
                    ItemPickUpRequestFailed.ItemPickUpFailReason.ItemStacked => "Item stacked (already have similar).", // Clarify stacking
                    _ => $"Failed to pick up item (Reason Code: {response.FailReason})." // Fallback
                };
                _logger.LogWarning("❌ {ReasonText}", reasonText);
                // _networkManager.LastPickupSucceeded = false; _networkManager.PickupHandled = true; _networkManager.SignalMovementHandledIfWalking();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing ItemPickupFailed (22, FF). Packet: {PacketData}", Convert.ToHexString(packet.Span));
                // _networkManager.LastPickupSucceeded = false; _networkManager.PickupHandled = true; _networkManager.SignalMovementHandledIfWalking();
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x28, PacketRouter.NoSubCode)] // ItemRemoved
        public Task HandleItemRemovedAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ItemRemoved.Length) { _logger.LogWarning("⚠️ Received ItemRemoved packet (0x28) with unexpected length {Length}.", packet.Length); return Task.CompletedTask; }
                var itemRemoved = new ItemRemoved(packet);
                // TODO: Ideally, get item name BEFORE removing it from state for a better log message
                // string itemName = _characterState.GetItemNameBySlot(itemRemoved.InventorySlot) ?? "Unknown Item";
                _characterState.RemoveInventoryItem(itemRemoved.InventorySlot);
                _logger.LogInformation("🗑️ Item removed from inventory slot {Slot}.", itemRemoved.InventorySlot);
                // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
                // _networkManager.ViewModel.UpdateCharacterStateDisplay();
                // _networkManager.UpdateConsoleTitle();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ItemRemoved (0x28)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x2A, PacketRouter.NoSubCode)] // ItemDurabilityChanged
        public Task HandleItemDurabilityChangedAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ItemDurabilityChanged.Length) { _logger.LogWarning("⚠️ Received ItemDurabilityChanged packet (0x2A) with unexpected length {Length}.", packet.Length); return Task.CompletedTask; }
                var duraUpdate = new ItemDurabilityChanged(packet);
                // TODO: Get item name for better log message
                // string itemName = _characterState.GetItemNameBySlot(duraUpdate.InventorySlot) ?? "Unknown Item";
                _logger.LogInformation("🔧 Item durability in slot {Slot} changed to {Durability}.", duraUpdate.InventorySlot, duraUpdate.Durability);
                _characterState.UpdateItemDurability(duraUpdate.InventorySlot, duraUpdate.Durability); // Update state
                // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ItemDurabilityChanged (0x2A)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x26, 0xFD)] // ItemConsumptionFailed
        public Task HandleItemConsumptionFailedAsync(Memory<byte> packet)
        {
            try
            {
                uint currentHp = 0, currentSd = 0;
                if (packet.Length >= ItemConsumptionFailedExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new ItemConsumptionFailedExtended(packet);
                    currentHp = stats.Health; currentSd = stats.Shield; _logger.LogDebug("❗ Parsing ItemConsumptionFailed (Extended)");
                }
                else if (packet.Length >= ItemConsumptionFailed.Length)
                {
                    var stats = new ItemConsumptionFailed(packet);
                    currentHp = stats.Health; currentSd = stats.Shield; _logger.LogDebug("❗ Parsing ItemConsumptionFailed (Standard)");
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for ItemConsumptionFailed packet (26, FD).", packet.Length); return Task.CompletedTask; }
                _logger.LogWarning("❗ Item consumption failed. Current HP: {HP}, SD: {SD}", currentHp, currentSd);
                _characterState.UpdateCurrentHealthShield(currentHp, currentSd);
                // _networkManager.ViewModel.UpdateInventoryDisplay(); // Odśwież widok Inventory
                // _networkManager.ViewModel.UpdateStatsDisplay();
                // _networkManager.UpdateConsoleTitle();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ItemConsumptionFailed (26, FD)."); }
            return Task.CompletedTask;
        }

        // Add other inventory/item related handlers here (e.g., F3 14 InventoryItemUpgraded)
    }
}