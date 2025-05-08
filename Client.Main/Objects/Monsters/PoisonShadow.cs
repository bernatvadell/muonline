using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(39, "Poison Shadow")]
    public class PoisonShadow : ShadowMonster // Inherits from ShadowMonster
    {
        public PoisonShadow()
        {
            Scale = 1.2f; // Inherited scale
            // Inherited Alpha and BlendState
        }

        // Load() and sound methods inherited from ShadowMonster
    }
}