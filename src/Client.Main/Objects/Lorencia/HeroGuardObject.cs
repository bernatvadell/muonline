using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class HeroGuardObject : ModelObject
    {
        public HeroGuardObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            Debug.WriteLine($"Unknown BMD");
        }
    }
}
