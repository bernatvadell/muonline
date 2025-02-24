using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerArmorObject : ModelObject
    {
        private PlayerClass _playerClass;
        public PlayerClass PlayerClass { get => _playerClass; set { _playerClass = value; OnChangePlayerClass(); } }

        public PlayerArmorObject()
        {
            RenderShadow = true;
        }

        private async void OnChangePlayerClass()
        {
            Model = await BMDLoader.Instance.Prepare($"Player/ArmorClass{(int)PlayerClass:D2}.bmd");
            if (Model != null && Status == GameControlStatus.Error)
                Status = GameControlStatus.Ready;
        }
    }
}
