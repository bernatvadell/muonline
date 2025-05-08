using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(51, "Great Bahamut")]
    public class GreatBahamut : Bahamut // Inherits from Bahamut
    {
        public GreatBahamut()
        {
            Scale = 1.0f; // Set according to C++ Setting_Monster (same as Bahamut?)
        }
        // Load() and sounds inherited
    }
}