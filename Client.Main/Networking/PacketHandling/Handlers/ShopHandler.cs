using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Networking;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI;
using Client.Main.Scenes;
using Client.Main.Controls.UI.Game;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to NPC shops (StoreItemList etc.).
    /// </summary>
    public class ShopHandler : IGamePacketHandler
    {
        private readonly ILogger<ShopHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly TargetProtocolVersion _targetVersion;
        private NpcWindowResponse.NpcWindow? _lastWindow;

        public ShopHandler(
            ILoggerFactory loggerFactory,
            CharacterState characterState,
            NetworkManager networkManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<ShopHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        /// <summary>
        /// NpcWindowResponse: Open shop window when server requests a merchant window.
        /// </summary>
        [PacketHandler(0x30, PacketRouter.NoSubCode)]
        public Task HandleNpcWindowResponseAsync(Memory<byte> packet)
        {
            try
            {
                var resp = new NpcWindowResponse(packet);
                _lastWindow = resp.Window;

                if (resp.Window == NpcWindowResponse.NpcWindow.Merchant || resp.Window == NpcWindowResponse.NpcWindow.Merchant1)
                {
                    _characterState.ClearShopItems();

                    // Check if the NPC can repair items using the last NPC type number
                    bool canRepair = false;
                    if (_characterState.LastNpcTypeNumber != 0)
                    {
                        canRepair = NpcDatabase.CanNpcRepair(_characterState.LastNpcTypeNumber);
                        _logger.LogDebug("NPC type {TypeId} CanRepair = {CanRepair}",
                            _characterState.LastNpcTypeNumber, canRepair);
                    }

                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var shop = NpcShopControl.Instance;
                        shop.SetRepairShop(canRepair);
                        shop.Visible = true;
                        shop.BringToFront();

                        // Also open the inventory side-by-side
                        var scene = MuGame.Instance?.ActiveScene as GameScene;
                        scene?.InventoryControl?.Show();
                    });
                }
                else if (resp.Window == NpcWindowResponse.NpcWindow.VaultStorage)
                {
                    _characterState.ClearVaultItems();
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var vault = VaultControl.Instance;
                        vault.Visible = true;
                        vault.BringToFront();
                        var scene = MuGame.Instance?.ActiveScene as GameScene;
                        scene?.InventoryControl?.Show();
                    });
                }
                else if (resp.Window == NpcWindowResponse.NpcWindow.ChaosMachine)
                {
                    _characterState.ClearChaosMachineItems();
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var chaos = ChaosMixControl.Instance;
                        chaos.Show();

                        var scene = MuGame.Instance?.ActiveScene as GameScene;
                        scene?.InventoryControl?.Show();
                    });
                }
                else if (resp.Window == NpcWindowResponse.NpcWindow.DevilSquare)
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        DevilSquareEnterControl.Instance.ShowWindow();
                    });
                }
                else if (resp.Window == NpcWindowResponse.NpcWindow.BloodCastle)
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        BloodCastleEnterControl.Instance.ShowWindow();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing NpcWindowResponse packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// StoreItemList: Sent when opening merchant NPC or vault. Displays items in the opened window.
        /// </summary>
        [PacketHandler(0x31, PacketRouter.NoSubCode)]
        public Task HandleStoreItemListAsync(Memory<byte> packet)
        {
            try
            {
                var span = packet.Span;
                if (span.Length < 7)
                {
                    _logger.LogWarning("StoreItemList packet too short: {Length}", packet.Length);
                    return Task.CompletedTask;
                }

                var list = new StoreItemList(packet);
                byte count = list.ItemCount;
                int offset = 6;

                // Determine item data size by protocol version (matches inventory parsing)
                int dataSize = _targetVersion switch
                {
                    TargetProtocolVersion.Season6 => 12,
                    TargetProtocolVersion.Version097 => 11,
                    TargetProtocolVersion.Version075 => 7,
                    _ => 12
                };

                _logger.LogInformation("StoreItemList received: Type={Type}, Count={Count}, DataSize={Size}", list.Type, count, dataSize);

                bool toChaosMachine = _lastWindow == NpcWindowResponse.NpcWindow.ChaosMachine || list.Type == StoreItemList.ItemWindow.ChaosMachine;
                bool toVault = !toChaosMachine && _lastWindow == NpcWindowResponse.NpcWindow.VaultStorage;

                if (toChaosMachine)
                {
                    _characterState.ClearChaosMachineItems();
                }
                else if (toVault)
                {
                    _characterState.ClearVaultItems();
                }
                else
                {
                    _characterState.ClearShopItems();
                }

                int parsedItems = 0;

                if (_targetVersion >= TargetProtocolVersion.Season6)
                {
                    int pos = offset;
                    int remaining = Math.Max(0, span.Length - offset);
                    bool fixedLength = count > 0 && remaining == count * (1 + 12);

                    for (int i = 0; i < count; i++)
                    {
                        if (pos + 1 > span.Length)
                        {
                            _logger.LogWarning("StoreItemList too short while reading slot at item {Index}.", i);
                            break;
                        }

                        byte slot = span[pos];
                        pos += 1;

                        ReadOnlySpan<byte> itemSpan = span.Slice(pos);
                        int length = 12;

                        if (!fixedLength)
                        {
                            if (!ItemDataParser.TryGetExtendedItemLength(itemSpan, out length) || pos + length > span.Length)
                            {
                                // Backward compatibility with classic fixed-length store items.
                                if (pos + 12 <= span.Length)
                                {
                                    length = 12;
                                    _logger.LogDebug(
                                        "StoreItemList item {Index}: extended length parse failed, falling back to 12-byte item data.",
                                        i);
                                }
                                else
                                {
                                    _logger.LogWarning("StoreItemList item {Index} has invalid data length.", i);
                                    break;
                                }
                            }
                        }

                        if (pos + length > span.Length)
                        {
                            _logger.LogWarning("StoreItemList too short while reading item {Index} data.", i);
                            break;
                        }

                        var data = span.Slice(pos, length).ToArray();
                        pos += length;

                        if (toChaosMachine) _characterState.AddOrUpdateChaosMachineItem(slot, data);
                        else if (toVault) _characterState.AddOrUpdateVaultItem(slot, data);
                        else _characterState.AddOrUpdateShopItem(slot, data);

                        parsedItems++;
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        var si = list[i, StoredItem.GetRequiredSize(dataSize)];
                        byte slot = si.ItemSlot;
                        var data = si.ItemData.Slice(0, dataSize).ToArray();

                        if (toChaosMachine) _characterState.AddOrUpdateChaosMachineItem(slot, data);
                        else if (toVault) _characterState.AddOrUpdateVaultItem(slot, data);
                        else _characterState.AddOrUpdateShopItem(slot, data);

                        parsedItems++;
                    }
                }

                _logger.LogInformation(
                    "StoreItemList parsed: requested={Requested}, parsed={Parsed}, target={Target}",
                    count,
                    parsedItems,
                    toChaosMachine ? "ChaosMachine" : toVault ? "Vault" : "Shop");

                if (toChaosMachine) _characterState.RaiseChaosMachineItemsChanged();
                else if (toVault) _characterState.RaiseVaultItemsChanged();
                else _characterState.RaiseShopItemsChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing StoreItemList packet.");
            }
            return Task.CompletedTask;
        }

        [PacketHandler(0x86, PacketRouter.NoSubCode)] // ItemCraftingResult
        public Task HandleItemCraftingResultAsync(Memory<byte> packet)
        {
            try
            {
                var res = new ItemCraftingResult(packet);
                _logger.LogInformation("ItemCraftingResult: Result={Result}", res.Result);

                MuGame.ScheduleOnMainThread(() =>
                {
                    ChaosMixControl.Instance?.NotifyCraftingResult(res.Result);

                    var scene = MuGame.Instance?.ActiveScene as GameScene;
                    string msg = res.Result switch
                    {
                        ItemCraftingResult.CraftingResult.Success => "Mix succeeded.",
                        ItemCraftingResult.CraftingResult.Failed => "Mix failed.",
                        ItemCraftingResult.CraftingResult.NotEnoughMoney => "Not enough Zen.",
                        ItemCraftingResult.CraftingResult.TooManyItems => "Too many items in mix box.",
                        ItemCraftingResult.CraftingResult.CharacterLevelTooLow => "Character level too low.",
                        ItemCraftingResult.CraftingResult.LackingMixItems => "Missing required items.",
                        ItemCraftingResult.CraftingResult.IncorrectMixItems => "Incorrect mix items.",
                        ItemCraftingResult.CraftingResult.InvalidItemLevel => "Invalid item level.",
                        ItemCraftingResult.CraftingResult.CharacterClassTooLow => "Character class too low.",
                        _ => $"Mix result: {res.Result}"
                    };

                    scene?.ChatLog?.AddMessage("System", msg, Client.Main.Models.MessageType.System);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ItemCraftingResult packet.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle 0x32 (Buy) results. For this family, some packets have no subcode (ItemBought),
        /// and failure (NpcItemBuyFailed) is a short 4-byte packet which some servers send with subcode.
        /// We treat all 0x32 as 'no subcode' in the router and differentiate by length here.
        /// </summary>
        [PacketHandler(0x32, PacketRouter.NoSubCode)]
        public Task HandleNpcBuyResultsAsync(Memory<byte> packet)
        {
            try
            {
                // Failure case: C1 04 32 FF (len = 4)
                if (packet.Length == NpcItemBuyFailed.Length)
                {
                    _logger.LogWarning("NPC item buy failed. Packet len={Len}", packet.Length);
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        string reason = ChatMessageHandler.TryGetRecentBlueSystemMessage(2000);
                        if (string.IsNullOrWhiteSpace(reason))
                        {
                            reason = "Purchase failed. Please check Zen or space.";
                        }
                        RequestDialog.ShowInfo(reason);
                    });
                    return Task.CompletedTask;
                }

                // Success case: ItemBought (C1 0x32, no subcode). Contains inventory slot + item data
                var bought = new ItemBought(packet);
                byte slot = bought.InventorySlot;
                var data = bought.ItemData.ToArray();

                _characterState.AddOrUpdateInventoryItem(slot, data);
                _logger.LogInformation("Item bought to inventory slot {Slot}: {Name}", slot, ItemDatabase.GetItemName(data) ?? "Item");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling 0x32 buy result packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles result of sell item to NPC (0x33). Updates money is handled elsewhere; here we cleanup inventory.
        /// </summary>
        [PacketHandler(0x33, PacketRouter.NoSubCode)]
        public Task HandleNpcItemSellResultAsync(Memory<byte> packet)
        {
            try
            {
                var res = new NpcItemSellResult(packet);
                _logger.LogInformation("NpcItemSellResult: Success={Success}, Money={Money}", res.Success, res.Money);
                if (res.Success)
                {
                    if (_characterState.TryConsumePendingSellSlot(out byte soldSlot))
                    {
                        _characterState.RemoveInventoryItem(soldSlot);
                    }
                    else
                    {
                        // No pending slot tracked; trigger a full refresh to stay in sync
                        _characterState.RaiseInventoryChanged();
                    }
                }
                else
                {
                    // Sell failed; just raise refresh to restore UI state, if needed
                    _characterState.RaiseInventoryChanged();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling NpcItemSellResult packet.");
            }
            return Task.CompletedTask;
        }
    }
}
