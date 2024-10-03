using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(ModelType.MurdererDog)]
    public class MurdererDogObject : WorldObject
    {
        public MurdererDogObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Debug.WriteLine("Unknown BMD");
        }
    }
}
