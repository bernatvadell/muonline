using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class HangingObject : ModelObject
    {
        public HangingObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Hanging01.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
