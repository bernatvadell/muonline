using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(min: ModelType.Face01, max: ModelType.Face07)]
    public class FaceObject : WorldObject
    {
        public FaceObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Face01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Logo/NewFace{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
