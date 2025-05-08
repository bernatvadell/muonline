using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(43, "Golden Budge Dragon")]
    public class GoldenBudgeDragon : BudgeDragon // Inherits from BudgeDragon
    {
        public GoldenBudgeDragon()
        {
            Scale = 0.7f; // Set according to C++ Setting_Monster
        }
        // Load() and sounds inherited
    }
}