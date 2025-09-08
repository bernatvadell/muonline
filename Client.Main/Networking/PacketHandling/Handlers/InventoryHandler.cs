using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;
using System;
using System.Threading.Tasks;
using Client.Main.Core.Client;
using Client.Main.Controllers;
using System.Linq;
using Client.Main.Controls.UI;
using Client.Main.Models;
using Client.Main.Controls;
using Client.Main.Objects;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to inventory, items, and money updates.
    /// </summary>
    public class InventoryHandler : IGamePacketHandler
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly ILogger<InventoryHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly TargetProtocolVersion _targetVersion;

        // ───────────────────────── Constructors ─────────────────────────
        public InventoryHandler(
            ILoggerFactory loggerFactory,
            CharacterState characterState,
            NetworkManager networkManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<InventoryHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        // ─────────────────────── Packet Handlers ────────────────────────

        [PacketHandler(0xF3, 0x10)]  // CharacterInventory
        public Task HandleCharacterInventoryAsync(Memory<byte> packet)
        {
            try
            {
                UpdateInventoryFromPacket(packet.Span);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CharacterInventory packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x22, 0xFE)]  // InventoryMoneyUpdate
        public Task HandleInventoryMoneyUpdateAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < InventoryMoneyUpdate.Length)
                {
                    _logger.LogWarning("InventoryMoneyUpdate packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var moneyUpdate = new InventoryMoneyUpdate(packet);
                _logger.LogInformation("Inventory Zen Updated: {Amount}", moneyUpdate.Money);
                _characterState.UpdateInventoryZen(moneyUpdate.Money);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing InventoryMoneyUpdate packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x22, 0x01)]  // ItemAddedToInventory
        public Task HandleItemAddedToInventoryAsync(Memory<byte> packet)
        {
            byte headerType = packet.Span[0];
            try
            {
                int minLength = (headerType == 0xC1 || headerType == 0xC3) ? 4 : 5;
                if (packet.Length < minLength + 1)
                {
                    _logger.LogWarning("ItemAddedToInventory packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var itemAdded = new ItemAddedToInventory(packet);
                var data = itemAdded.ItemData;
                if (data.Length == 0)
                {
                    _logger.LogWarning("ItemAddedToInventory contains no data.");
                    return Task.CompletedTask;
                }

                _characterState.AddOrUpdateInventoryItem(itemAdded.InventorySlot, data.ToArray());
                string itemName = ItemDatabase.GetItemName(data) ?? "Unknown Item";
                _logger.LogInformation("Picked up '{ItemName}' into slot {Slot}.", itemName, itemAdded.InventorySlot);
                SoundController.Instance.PlayBuffer("Sound/pGetItem.wav");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ItemAddedToInventory packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x22, PacketRouter.NoSubCode)]  // Catches 0x22 with subcodes not 0x01, or if no subcode, effectively 0xFF
        public Task HandleItemPickupFailedAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < 4) // C3 Size Code SubCode/FailReasonByte
                {
                    _logger.LogWarning("ItemPickupFailed-like packet (0x22) too short: {Length}", packet.Length);
                    _characterState.ClearPendingPickedItem(); // Clean up just in case
                    ResetPendingPickupObject();
                    return Task.CompletedTask;
                }

                // The byte at index 3 is the sub-code, which in some cases is the FailReason or a slot index.
                byte subCodeOrSlotByte = packet.Span[3];
                var failReasonEnum = (ItemPickUpRequestFailed.ItemPickUpFailReason)subCodeOrSlotByte;

                string messageToUser = string.Empty;
                MessageType uiMessageType = MessageType.System;

                if (failReasonEnum == ItemPickUpRequestFailed.ItemPickUpFailReason.ItemStacked) // 0xFD
                {
                    _logger.LogInformation("Item pick-up reported as 'Stacked'. Item should be updated by another packet (e.g., durability or inventory list).");
                    _characterState.ClearPendingPickedItem();
                    ResetPendingPickupObject();
                    SoundController.Instance.PlayBuffer("Sound/pGetItem.wav");
                }
                else if (failReasonEnum == ItemPickUpRequestFailed.ItemPickUpFailReason.General) // 0xFF
                {
                    messageToUser = "Item pick-up failed.";
                    _characterState.ClearPendingPickedItem();
                    ResetPendingPickupObject();
                }
                else if (failReasonEnum == ItemPickUpRequestFailed.ItemPickUpFailReason.__MaximumInventoryMoneyReached) // 0xFE
                {
                    // This case for money should ideally be handled by InventoryMoneyUpdate.
                    // If it comes here, it's likely an error or an old server behavior.
                    messageToUser = "Your inventory is full (money limit reached).";
                    _characterState.ClearPendingPickedItem();
                    ResetPendingPickupObject();
                }
                else // The byte is NOT a known FailReason enum value, assume it's a slot index for SUCCESS.
                {
                    // This packet structure is now like ItemAddedToInventory: C3 Size 22 Slot ItemData...
                    // packet.Span[3] is the slot. ItemData starts at packet.Span[4].
                    byte targetSlot = subCodeOrSlotByte;
                    ReadOnlySpan<byte> itemDataSpan = packet.Span.Slice(4); // Item data starts AFTER the slot byte.

                    if (itemDataSpan.IsEmpty)
                    {
                        _logger.LogWarning("Item pickup 'success' (0x22, Slot: {Slot}) received, but item data is empty.", targetSlot);
                        messageToUser = "Item pick-up error (missing item data).";
                        _characterState.ClearPendingPickedItem();
                        ResetPendingPickupObject();
                    }
                    // Attempt to commit the stashed item.
                    else if (_characterState.CommitStashedItem(targetSlot))
                    {
                        // Successfully committed the stashed item.
                        // The item name is now available from the updated CharacterState.
                        string itemName = "Unknown Item";
                        if (_characterState.GetInventoryItems().TryGetValue(targetSlot, out var committedItemData))
                        {
                            itemName = ItemDatabase.GetItemName(committedItemData) ?? "Item";
                        }
                        _logger.LogInformation("Item '{ItemName}' picked up successfully into slot {Slot} (via 0x22, SubCode={SubCode:X2} interpreted as slot).", itemName, targetSlot, targetSlot);
                        SoundController.Instance.PlayBuffer("Sound/pGetItem.wav");
                        messageToUser = $"Item '{itemName}' picked up successfully into slot {targetSlot}";
                        ResetPendingPickupObject();
                    }
                    else
                    {
                        // CommitStashedItem returned false, likely because _pendingPickedItem was null.
                        _logger.LogError("Failed to commit stashed item for slot {Slot} after receiving 'success' type message (0x22, SubCode={SubCode:X2}). No pending item data was stashed by client?", targetSlot, targetSlot);
                        messageToUser = "Item pick-up error (client state issue).";
                        // _pendingPickedItem is already null or was never set, so ClearPendingPickedItem is effectively done.
                        // We still call it to ensure the state is clean if it somehow wasn't null.
                        _characterState.ClearPendingPickedItem();
                        ResetPendingPickupObject();
                    }
                }

                // If an error message was set, display it.
                if (!string.IsNullOrEmpty(messageToUser))
                {
                    _logger.LogWarning("Item Pickup Issue: {Message}", messageToUser);
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                        gameScene?.Controls.OfType<ChatLogWindow>().FirstOrDefault()?.AddMessage("System", messageToUser, uiMessageType);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ItemPickupFailed-like packet (0x22).");
                _characterState.ClearPendingPickedItem(); // Ensure cleanup on any exception.
                ResetPendingPickupObject();
            }
            return Task.CompletedTask;
        }

        private void ResetPendingPickupObject()
        {
            ushort? rawId = _characterState.PendingPickupRawId;
            _characterState.ClearPendingPickupRawId();
            if (!rawId.HasValue)
                return;

            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene?.World is not WalkableWorldControl world)
                    return;

                ushort masked = (ushort)(rawId.Value & 0x7FFF);
                var obj = world.Objects.OfType<DroppedItemObject>().FirstOrDefault(d => d.NetworkId == masked);
                obj?.ResetPickupState();
            });
        }

        [PacketHandler(0x28, PacketRouter.NoSubCode)]  // ItemRemoved
        public Task HandleItemRemovedAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ItemRemoved.Length)
                {
                    _logger.LogWarning("ItemRemoved packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var removed = new ItemRemoved(packet);
                _characterState.RemoveInventoryItem(removed.InventorySlot);
                _logger.LogInformation("Removed item from slot {Slot}.", removed.InventorySlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ItemRemoved packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x24, PacketRouter.NoSubCode)]  // ItemMoved / ItemMoveRequestFailed
        public Task HandleItemMovedOrFailedAsync(Memory<byte> packet)
        {
            try
            {
                var span = packet.Span;
                if (span.Length < 5)
                {
                    _logger.LogWarning("ItemMoved/Failed packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                bool isFailed = span[0] == 0xC3 && span.Length >= 4 && span[3] == 0xFF;
                if (isFailed)
                {
                    var failed = new ItemMoveRequestFailed(packet);
                    var itemData = failed.ItemData.ToArray();
                    // First, try restore inventory<->inventory failed move
                    if (_characterState.TryConsumePendingInventoryMove(out var fromSlot, out var toSlot))
                    {
                        // Restore the item at original location
                        _characterState.AddOrUpdateInventoryItem(fromSlot, itemData);
                        // Ensure the temporary target slot is clean
                        if (fromSlot != toSlot)
                        {
                            _characterState.RemoveInventoryItem(toSlot);
                        }
                        _logger.LogWarning("Item move failed. Restored item at slot {From}", fromSlot);
                        // Inform user
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                            gameScene?.Controls.OfType<Client.Main.Controls.UI.ChatLogWindow>()
                                .FirstOrDefault()?.AddMessage("System", "Moving the item failed.", Client.Main.Models.MessageType.Error);
                        });
                    }
                    // Then, try restore any pending storage move (vault <-> inventory)
                    else if (_characterState.TryConsumePendingVaultMove(out var vFrom, out var vTo))
                    {
                        // If the move originated from vault -> inventory, vFrom represents the vault slot and vTo == 0xFF
                        // Restore the item back into the vault at its original slot.
                        _characterState.AddOrUpdateVaultItem(vFrom, itemData);
                        _characterState.RaiseVaultItemsChanged();
                        _logger.LogWarning("Storage move failed. Restored vault item at slot {From}", vFrom);

                        // As a best-effort, also refresh inventory UI so any temporary visuals get reset.
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            _characterState.RaiseInventoryChanged();
                            var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                            gameScene?.Controls.OfType<Client.Main.Controls.UI.ChatLogWindow>()
                                .FirstOrDefault()?.AddMessage("System", "Moving the item failed.", Client.Main.Models.MessageType.Error);
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Item move failed, but no pending move was stashed by client.");
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                            gameScene?.Controls.OfType<Client.Main.Controls.UI.ChatLogWindow>()
                                .FirstOrDefault()?.AddMessage("System", "Moving the item failed.", Client.Main.Models.MessageType.Error);
                        });
                    }
                }
                else
                {
                    var moved = new ItemMoved(packet);
                    var targetStore = moved.TargetStorageType;
                    byte toSlot = moved.TargetSlot;
                    var itemData = moved.ItemData.ToArray();

                    if (targetStore == ItemStorageKind.Vault)
                    {
                        // Clear any pending storage move information
                        if (_characterState.TryConsumePendingVaultMove(out var from, out var to))
                        {
                            if (from != to)
                            {
                                _characterState.RemoveVaultItem(from);
                            }
                        }
                        // If the move originated from Inventory -> Vault, remove the source inventory slot now.
                        if (_characterState.TryConsumePendingInventoryMove(out var invFrom, out var _))
                        {
                            _characterState.RemoveInventoryItem(invFrom);
                        }

                        _characterState.AddOrUpdateVaultItem(toSlot, itemData);
                        _characterState.RaiseVaultItemsChanged();
                        _logger.LogInformation("Vault item moved to slot {To}.", toSlot);
                    }
                    else // Inventory or others -> inventory fallback
                    {
                        // If we had a pending vault->inventory move, remove from vault
                        if (_characterState.TryConsumePendingVaultMove(out var vf, out var vt))
                        {
                            _characterState.RemoveVaultItem(vf);
                            _characterState.RaiseVaultItemsChanged();
                        }
                        if (_characterState.TryConsumePendingInventoryMove(out var fromSlot, out var pendingTo))
                        {
                            if (fromSlot != toSlot)
                            {
                                _characterState.RemoveInventoryItem(fromSlot);
                            }
                            _characterState.AddOrUpdateInventoryItem(toSlot, itemData);
                            _logger.LogInformation("Item moved: {From} -> {To}", fromSlot, toSlot);
                        }
                        else
                        {
                            // No pending move stashed; best effort update
                            _characterState.AddOrUpdateInventoryItem(toSlot, itemData);
                            _logger.LogInformation("Item moved to slot {To}, no pending move available.", toSlot);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ItemMoved/Failed (0x24) packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x23, PacketRouter.NoSubCode)]  // DropItemRequest result (ack)
        public Task HandleDropItemAckAsync(Memory<byte> packet)
        {
            try
            {
                var span = packet.Span;
                if (span.Length < 5 || span[0] != 0xC1 || span[2] != 0x23)
                {
                    _logger.LogWarning("Unexpected DropItem ack format. Data: {Data}", Convert.ToHexString(span));
                    return Task.CompletedTask;
                }

                bool success = span[3] != 0;
                byte slot = span[4];
                if (success)
                {
                    _characterState.RemoveInventoryItem(slot);
                    _logger.LogInformation("DropItem ACK: Removed slot {Slot} from inventory.", slot);
                }
                else
                {
                    _logger.LogWarning("DropItem ACK: Server reported failure for slot {Slot}", slot);
                    // Force refresh to re-show the item and inform user
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        _characterState.RaiseInventoryChanged();
                        var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                        gameScene?.Controls.OfType<Client.Main.Controls.UI.ChatLogWindow>()
                            .FirstOrDefault()?.AddMessage("System", "Dropping the item failed.", Client.Main.Models.MessageType.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing DropItem ack (0x23).");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x2A, PacketRouter.NoSubCode)]  // ItemDurabilityChanged
        public Task HandleItemDurabilityChangedAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < ItemDurabilityChanged.Length)
                {
                    _logger.LogWarning("ItemDurabilityChanged packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }
                var update = new ItemDurabilityChanged(packet);
                _characterState.UpdateItemDurability(update.InventorySlot, update.Durability);
                _logger.LogInformation("Durability updated for slot {Slot}: {Durability}", update.InventorySlot, update.Durability);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ItemDurabilityChanged packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x26, 0xFD)]  // ItemConsumptionFailed
        public Task HandleItemConsumptionFailedAsync(Memory<byte> packet)
        {
            try
            {
                uint hp = 0, sd = 0;
                bool extended = packet.Length >= ItemConsumptionFailedExtended.Length && _targetVersion >= TargetProtocolVersion.Season6;
                if (extended)
                {
                    var stats = new ItemConsumptionFailedExtended(packet);
                    hp = stats.Health;
                    sd = stats.Shield;
                    _logger.LogDebug("Parsing ItemConsumptionFailed (Extended).");
                }
                else if (packet.Length >= ItemConsumptionFailed.Length)
                {
                    var stats = new ItemConsumptionFailed(packet);
                    hp = stats.Health;
                    sd = stats.Shield;
                    _logger.LogDebug("Parsing ItemConsumptionFailed (Standard).");
                }
                else
                {
                    _logger.LogWarning("ItemConsumptionFailed packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.UpdateCurrentHealthShield(hp, sd);
                _logger.LogWarning("Item consumption failed. Current HP: {HP}, SD: {SD}", hp, sd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ItemConsumptionFailed packet.");
            }
            return Task.CompletedTask;
        }

        // ────────────────────────── Helpers ────────────────────────────

        /// <summary>
        /// Parses the inventory packet and updates CharacterState accordingly.
        /// </summary>
        private void UpdateInventoryFromPacket(ReadOnlySpan<byte> span)
        {
            _characterState.ClearInventory();

            int count = 0, offset = 0, dataSize = 0;
            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var invS6 = new CharacterInventoryRef(span.ToArray());
                    count = invS6.ItemCount;
                    offset = 6;
                    dataSize = 12;
                    _logger.LogInformation("Updating inventory (S6): {Count} items.", count);
                    break;

                case TargetProtocolVersion.Version097:
                    if (span[0] != 0xC1 || span[2] != 0x10)
                    {
                        _logger.LogWarning("Unexpected packet instead of 0.97 inventory.");
                        return;
                    }
                    count = span[4];
                    offset = 5;
                    dataSize = 11;
                    _logger.LogInformation("Updating inventory (0.97): {Count} items.", count);
                    break;

                case TargetProtocolVersion.Version075:
                    if (span[0] != 0xC1 || span[2] != 0x10)
                    {
                        _logger.LogWarning("Unexpected packet instead of 0.75 inventory.");
                        return;
                    }
                    count = span[4];
                    offset = 5;
                    dataSize = 7;
                    _logger.LogInformation("Updating inventory (0.75): {Count} items.", count);
                    break;

                default:
                    _logger.LogWarning("Inventory handling not implemented for version {Version}.", _targetVersion);
                    return;
            }

            int slotSize = 1 + dataSize;
            int pos = offset;
            for (int i = 0; i < count; i++, pos += slotSize)
            {
                if (pos + slotSize > span.Length)
                {
                    _logger.LogWarning("Inventory packet too short parsing item {Index}.", i);
                    break;
                }

                byte slot = span[pos];
                var itemData = span.Slice(pos + 1, dataSize).ToArray();
                _characterState.AddOrUpdateInventoryItem(slot, itemData);

                string name = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                _logger.LogDebug("Slot {Slot}: {Name} (DataLen: {Len})", slot, name, dataSize);
            }
        }
    }
}
