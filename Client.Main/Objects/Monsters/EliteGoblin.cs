using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(33, "Elite Goblin")]
    public class EliteGoblin : Goblin // Inherits from Goblin as it uses the same model/sounds
    {
        public EliteGoblin()
        {
            Scale = 1.2f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Uses the same model as Goblin
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster20.bmd");
            await base.Load();
            // Inherits sounds and playspeed adjustments from Goblin
        }
    }
}