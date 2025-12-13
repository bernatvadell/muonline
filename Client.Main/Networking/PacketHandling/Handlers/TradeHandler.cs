using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Networking;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI.Game.Trade;
using Client.Main.Scenes;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to player-to-player trading.
    /// </summary>
    public class TradeHandler : IGamePacketHandler
    {
        private readonly ILogger<TradeHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly TargetProtocolVersion _targetVersion;

        public TradeHandler(
            ILoggerFactory loggerFactory,
            CharacterState characterState,
            NetworkManager networkManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<TradeHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        /// <summary>
        /// TradeRequestAnswer (0x37): Server response when trade request is accepted.
        /// Opens trade window for both players.
        /// </summary>
        [PacketHandler(0x37, PacketRouter.NoSubCode)]
        public Task HandleTradeRequestAnswerAsync(Memory<byte> packet)
        {
            try
            {
                var answer = new TradeRequestAnswer(packet);
                string partnerName = answer.Name;
                ushort partnerLevel = answer.TradePartnerLevel;
                uint guildId = answer.GuildId;
                string partnerGuild = ""; // TODO: Resolve guild name from GuildId if needed
                bool accepted = answer.Accepted;

                if (!accepted)
                {
                    _logger.LogInformation("Trade request was declined by {Partner}.", partnerName);
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        MessageWindow.Show($"{partnerName} declined your trade request.");
                    });
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Trade request accepted. Opening trade window with {Partner} (Level: {Level})",
                    partnerName, partnerLevel);

                // Partner ID is not in the packet, using 0 as placeholder
                _characterState.StartTrade(0, partnerName, partnerLevel, partnerGuild);

                // Open trade window + inventory on main thread
                MuGame.ScheduleOnMainThread(() =>
                {
                    var scene = MuGame.Instance?.ActiveScene as GameScene;
                    if (scene?.TradeControl != null)
                    {
                        scene.TradeControl.Show();
                        scene.TradeControl.BringToFront();
                    }

                    // Auto-open inventory as requested
                    scene?.InventoryControl?.Show();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeRequestAnswer packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// TradeItemAdded (0x39): Partner added an item to the trade.
        /// </summary>
        [PacketHandler(0x39, PacketRouter.NoSubCode)]
        public Task HandleTradeItemAddedAsync(Memory<byte> packet)
        {
            try
            {
                var itemAdded = new TradeItemAdded(packet);
                byte slot = itemAdded.ToSlot;
                byte[] itemData = itemAdded.ItemData.ToArray();

                _logger.LogInformation("Trade partner added item to slot {Slot}", slot);
                _characterState.AddOrUpdatePartnerTradeItem(slot, itemData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeItemAdded packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// TradeItemRemoved (0x38): Partner removed an item from the trade.
        /// </summary>
        [PacketHandler(0x38, PacketRouter.NoSubCode)]
        public Task HandleTradeItemRemovedAsync(Memory<byte> packet)
        {
            try
            {
                var itemRemoved = new TradeItemRemoved(packet);
                byte slot = itemRemoved.Slot;

                _logger.LogInformation("Trade partner removed item from slot {Slot}", slot);
                _characterState.RemovePartnerTradeItem(slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeItemRemoved packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// TradeMoneySetResponse (0x3A, 0x01): Confirmation when we set money in trade.
        /// </summary>
        [PacketHandler(0x3A, 0x01)]
        public Task HandleTradeMoneySetResponseAsync(Memory<byte> packet)
        {
            try
            {
                _logger.LogInformation("Trade money set confirmed by server.");
                // No additional action needed, client already updated UI when user set money
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeMoneySetResponse packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// TradeMoneyUpdate (0x3B): Partner updated money amount in trade.
        /// </summary>
        [PacketHandler(0x3B, PacketRouter.NoSubCode)]
        public Task HandleTradeMoneyUpdateAsync(Memory<byte> packet)
        {
            try
            {
                var moneyUpdate = new TradeMoneyUpdate(packet);
                uint amount = moneyUpdate.MoneyAmount;

                _logger.LogInformation("Trade partner set money to {Amount}", amount);
                _characterState.SetPartnerTradeMoney(amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeMoneyUpdate packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// TradeButtonStateChanged (0x3C): Trade confirmation button state changed.
        /// </summary>
        [PacketHandler(0x3C, PacketRouter.NoSubCode)]
        public Task HandleTradeButtonStateChangedAsync(Memory<byte> packet)
        {
            try
            {
                var stateChanged = new TradeButtonStateChanged(packet);
                var state = stateChanged.State;

                _logger.LogInformation("Trade button state changed to {State}", state);
                if (state == TradeButtonStateChanged.TradeButtonState.Red)
                {
                    // Matches original client behavior: red state is a temporary warning which resets both accept states.
                    _characterState.SetMyTradeButtonState(TradeButtonStateChanged.TradeButtonState.Red);
                    _characterState.SetPartnerTradeButtonState(TradeButtonStateChanged.TradeButtonState.Unchecked);
                }
                else
                {
                    _characterState.SetPartnerTradeButtonState(state);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeButtonStateChanged packet.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// TradeFinished (0x3D): Trade completed or cancelled.
        /// </summary>
        [PacketHandler(0x3D, PacketRouter.NoSubCode)]
        public Task HandleTradeFinishedAsync(Memory<byte> packet)
        {
            try
            {
                var finished = new TradeFinished(packet);
                var result = finished.Result;

                _logger.LogInformation("Trade finished with result: {Result}", result);
                _characterState.EndTrade(result);

                // Show result message on main thread
                MuGame.ScheduleOnMainThread(() =>
                {
                    string message = result switch
                    {
                        TradeFinished.TradeResult.Success => "Trade completed successfully!",
                        TradeFinished.TradeResult.Cancelled => "Trade was cancelled.",
                        TradeFinished.TradeResult.FailedByFullInventory => "Trade failed: Inventory is full.",
                        TradeFinished.TradeResult.TimedOut => "Trade timed out.",
                        TradeFinished.TradeResult.FailedByItemsNotAllowedToTrade => "Trade failed: One or more items cannot be traded.",
                        _ => "Trade ended."
                    };

                    MessageWindow.Show(message);

                    // Close trade window
                    var scene = MuGame.Instance?.ActiveScene as GameScene;
                    scene?.TradeControl?.Hide();

                    // Refresh inventory to show new items (if success)
                    if (result == TradeFinished.TradeResult.Success)
                    {
                        _characterState.RaiseInventoryChanged();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TradeFinished packet.");
            }
            return Task.CompletedTask;
        }
    }
}
