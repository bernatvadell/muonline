using System;
using System.Threading.Tasks;
using Client.Main.Controls.UI.Game;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI.Game.Trade;
using Microsoft.Extensions.Logging;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneWindowCloseController
    {
        private readonly InventoryControl _inventoryControl;
        private readonly ILogger _logger;

        public GameSceneWindowCloseController(InventoryControl inventoryControl, ILogger logger)
        {
            _inventoryControl = inventoryControl;
            _logger = logger;
        }

        public void OnHeroMoved(object sender, EventArgs e)
        {
            if (NpcShopControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero moved, closing NPC shop window.");
                NpcShopControl.Instance.Visible = false;
                CloseInventoryIfVisible();
                SendCloseNpcRequestAsync();
            }

            if (VaultControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero moved, closing Vault (storage) window.");
                VaultControl.Instance.CloseWindow();
                CloseInventoryIfVisible();
                SendCloseNpcRequestAsync();
            }

            if (ChaosMixControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero moved, closing Chaos Mix window.");
                bool closed = ChaosMixControl.Instance.CloseWindow();
                if (closed)
                {
                    CloseInventoryIfVisible();
                }
            }

            if (TradeControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero moved, cancelling trade.");
                TradeControl.Instance.Hide();
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendTradeCancelAsync();
                }
            }
        }

        public void OnHeroTookDamage(object sender, EventArgs e)
        {
            if (VaultControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero took damage, closing Vault (storage) window.");
                VaultControl.Instance.CloseWindow();
                CloseInventoryIfVisible();
                SendCloseNpcRequestAsync();
            }

            if (ChaosMixControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero took damage, closing Chaos Mix window.");
                bool closed = ChaosMixControl.Instance.CloseWindow();
                if (closed)
                {
                    CloseInventoryIfVisible();
                }
            }

            if (TradeControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero took damage, cancelling trade.");
                TradeControl.Instance.Hide();
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendTradeCancelAsync();
                }
            }
        }

        private void CloseInventoryIfVisible()
        {
            if (_inventoryControl?.Visible == true)
            {
                _inventoryControl.Hide();
            }
        }

        private void SendCloseNpcRequestAsync()
        {
            var svc = MuGame.Network?.GetCharacterService();
            if (svc == null)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await svc.SendCloseNpcRequestAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send close NPC request");
                }
            });
        }
    }
}
