using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(35, "Death Gorgon")]
    public class DeathGorgon : Gorgon // Inherits from Gorgon
    {
        public DeathGorgon()
        {
            Scale = 1.3f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Uses the same model as Gorgon
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster12.bmd");
            await base.Load();
            // Inherits sounds and playspeed adjustments from Gorgon
        }
    }
}