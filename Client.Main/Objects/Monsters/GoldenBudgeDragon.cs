using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Extensions;
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

        public override async Task Load()
        {
            await base.Load();
            
            // Apply intense golden glow effect
            this.SetGoldGlow(2.5f);
        }
        // Sounds inherited
    }
}