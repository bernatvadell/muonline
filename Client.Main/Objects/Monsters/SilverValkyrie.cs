using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(52, "Silver Valkyrie")]
    public class SilverValkyrie : Valkyrie // Inherits from Valkyrie
    {
        public SilverValkyrie()
        {
            Scale = 1.4f; // Set according to C++ Setting_Monster
        }
        // Load() and sounds inherited
    }
}