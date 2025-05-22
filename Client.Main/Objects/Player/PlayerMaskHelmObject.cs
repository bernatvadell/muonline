using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerMaskHelmObject : ModelObject
    {
        private PlayerClass _playerClass;
        public PlayerClass PlayerClass
        {
            get => _playerClass;
            set { _playerClass = value; } // Only set the field, no async logic here
        }

        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<PlayerObject>();

        // New async setter for correct model loading
        public async Task SetPlayerClassAsync(PlayerClass playerClass)
        {
            if (_playerClass != playerClass)
            {
                _playerClass = playerClass;
                await OnChangePlayerClass();
            }
            else
            {
                _logger?.LogDebug($"SetPlayerClass Warning: Attempted to set class on a null part.");
            }
        }

        public PlayerMaskHelmObject()
        {
            RenderShadow = true; // Or false if it shouldn't cast shadows
        }

        // Now returns Task, not void
        private async Task OnChangePlayerClass()
        {
            // Determine path based on class, potentially male/female if needed
            // Assuming a single file per class for now
            Model = await BMDLoader.Instance.Prepare($"Player/MaskHelmMale{(int)PlayerClass:D2}.bmd"); // Adjust path as needed
            if (Model != null && Status == GameControlStatus.Error)
                Status = GameControlStatus.Ready;
            else if (Model == null)
            {
                _logger?.LogDebug($"PlayerMaskHelmObject: Failed to load model for PlayerClass {(int)PlayerClass}. Path: Player/MaskHelmMale{(int)PlayerClass:D2}.bmd");
                Status = GameControlStatus.Error;
            }
        }
    }
}