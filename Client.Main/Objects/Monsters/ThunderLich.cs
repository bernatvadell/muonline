using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(9, "Thunder Lich")]
    public class ThunderLich : Lich // Inherits from Lich
    {
        public ThunderLich()
        {
            Scale = 1.1f; // Set according to C++ Setting_Monster
        }
        // Load() and sound methods inherited
    }
}