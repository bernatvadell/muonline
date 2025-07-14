using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(258, "Luke the Helper")]
    public class LukeTheHelper : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10); // Plate Set
            await base.Load();
            CurrentAction = (int)PlayerAction.PlayerStopMale;
        }
        protected override void HandleClick() { }
    }
}
