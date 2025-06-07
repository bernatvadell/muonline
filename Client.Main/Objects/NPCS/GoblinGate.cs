using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(234, "Goblin Gate")]
    public class GoblinGate : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Monster/Monster20.bmd"); // Goblin model
            // Weapon1.Type = (int)ModelType.Staff + MODEL_ITEM; // Staff
            // Weapon1.Level = 4;
            Scale = 1.5f;
            await base.Load();
        }
        protected override void HandleClick() { }
    }
}
