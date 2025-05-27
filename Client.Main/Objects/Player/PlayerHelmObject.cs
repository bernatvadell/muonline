using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerHelmObject : ModelObject
    {
        private PlayerClass _playerClass;
        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<PlayerObject>();

        public PlayerClass PlayerClass
        {
            get => _playerClass;
            set { _playerClass = value; } // Only set the field, no async logic here
        }

        // New async setter for correct model loading
        public async Task SetPlayerClassAsync(PlayerClass playerClass)
        {
            if (_playerClass != playerClass)
            {
                _playerClass = playerClass;
                await OnChangePlayerClass();
            }
        }

        public PlayerHelmObject()
        {
            RenderShadow = true;
            // Initial class might be set later by PlayerObject
        }

        // Now returns Task, not void
        private async Task OnChangePlayerClass()
        {
            // Uses the PlayerClass enum value (e.g., 1 for DW) and formats it to two digits (01)
            Model = await BMDLoader.Instance.Prepare($"Player/HelmClass{(int)PlayerClass:D2}.bmd");
            if (Model != null && Status == GameControlStatus.Error)
                Status = GameControlStatus.Ready;
            else if (Model == null)
            {
                // Explicitly log if model failed to load for this specific class
                _logger?.LogDebug($"PlayerHelmObject: Failed to load model for PlayerClass {(int)PlayerClass}. Path: Player/HelmClass{(int)PlayerClass:D2}.bmd");
                Status = GameControlStatus.Error; // Keep error status if model is null
            }
        }
    }
}