using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerGloveObject : ModelObject
    {
        private PlayerClass _playerClass;
        public PlayerClass PlayerClass
        {
            get => _playerClass;
            set { if (_playerClass != value) { _playerClass = value; OnChangePlayerClass(); } }
        }

        public PlayerGloveObject()
        {
            RenderShadow = true;
        }

        private async void OnChangePlayerClass()
        {
            Model = await BMDLoader.Instance.Prepare($"Player/GloveClass{(int)PlayerClass:D2}.bmd");
            if (Model != null && Status == GameControlStatus.Error)
                Status = GameControlStatus.Ready;
            else if (Model == null)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerGloveObject: Failed to load model for PlayerClass {(int)PlayerClass}. Path: Player/GloveClass{(int)PlayerClass:D2}.bmd");
                Status = GameControlStatus.Error;
            }
        }
    }
}