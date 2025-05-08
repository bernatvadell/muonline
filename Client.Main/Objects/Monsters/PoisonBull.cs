using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(8, "Poison Bull")]
    public class PoisonBull : BullFighter // Inherits from BullFighter
    {
        public PoisonBull()
        {
            Scale = 1.0f; // Set according to C++ Setting_Monster
        }
        // Load() and sound methods inherited
    }
}