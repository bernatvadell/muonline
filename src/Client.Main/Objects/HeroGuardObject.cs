using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.HeroGuard)]
    public class HeroGuardObject : ModelObject
    {
        public HeroGuardObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Debug.WriteLine($"Unknown BMD");
        }
    }
}
