using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(50, "Sea Worm")]
    public class SeaWorm : MonsterObject
    {
        public SeaWorm()
        {
            RenderShadow = true;
            Scale = 1.8f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 38 -> File Number: 38 + 1 = 39
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster39.bmd");
            await base.Load();
            // No specific PlaySpeed adjustments mentioned
        }
        // No sounds assigned in C++ Setting_Monster
    }
}