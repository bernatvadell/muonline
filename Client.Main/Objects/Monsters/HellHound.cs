using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(5, "Hell Hound")]
    public class HellHound : Hound // Inherits from Hound as it uses the same model/sounds
    {
        public HellHound()
        {
            // Override scale if needed, base constructor sets it to 0.85f
            Scale = 1.1f; // Set according to C++ Setting_Monster
        }

        // Load() and sound methods are inherited from Hound
    }
}