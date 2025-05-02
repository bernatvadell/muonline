using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerHelmObject : ModelObject
    {
        private PlayerClass _playerClass;
        public PlayerClass PlayerClass
        {
            get => _playerClass;
            set { if (_playerClass != value) { _playerClass = value; OnChangePlayerClass(); } }
        }

        public PlayerHelmObject()
        {
            RenderShadow = true;
            // Initial class might be set later by PlayerObject
        }

        private async void OnChangePlayerClass()
        {
            // Uses the PlayerClass enum value (e.g., 1 for DW) and formats it to two digits (01)
            Model = await BMDLoader.Instance.Prepare($"Player/HelmClass{(int)PlayerClass:D2}.bmd");
            if (Model != null && Status == GameControlStatus.Error)
                Status = GameControlStatus.Ready;
            else if (Model == null)
            {
                // Explicitly log if model failed to load for this specific class
                System.Diagnostics.Debug.WriteLine($"PlayerHelmObject: Failed to load model for PlayerClass {(int)PlayerClass}. Path: Player/HelmClass{(int)PlayerClass:D2}.bmd");
                Status = GameControlStatus.Error; // Keep error status if model is null
            }
        }
    }
}