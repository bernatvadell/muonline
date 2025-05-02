using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerMaskHelmObject : ModelObject
    {
        private PlayerClass _playerClass;
        public PlayerClass PlayerClass
        {
            get => _playerClass;
            set { if (_playerClass != value) { _playerClass = value; OnChangePlayerClass(); } }
        }


        public PlayerMaskHelmObject()
        {
            RenderShadow = true; // Or false if it shouldn't cast shadows
        }

        private async void OnChangePlayerClass()
        {
            // Determine path based on class, potentially male/female if needed
            // Assuming a single file per class for now
            Model = await BMDLoader.Instance.Prepare($"Player/MaskHelmMale{(int)PlayerClass:D2}.bmd"); // Adjust path as needed
            if (Model != null && Status == GameControlStatus.Error)
                Status = GameControlStatus.Ready;
            else if (Model == null)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerMaskHelmObject: Failed to load model for PlayerClass {(int)PlayerClass}. Path: Player/MaskHelmMale{(int)PlayerClass:D2}.bmd");
                Status = GameControlStatus.Error;
            }
        }
    }
}