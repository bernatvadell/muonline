using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(4, "Elite Bull Fighter")]
    public class EliteBullFighter : BullFighter // Inherits from BullFighter as it uses the same model/sounds
    {
        public EliteBullFighter()
        {
            // Override scale if needed, base constructor sets it to 0.8f
            Scale = 1.15f; // Set according to C++ Setting_Monster
        }

        // Load() and sound methods are inherited from BullFighter
    }
}