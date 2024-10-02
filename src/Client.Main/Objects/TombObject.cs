using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(ModelType.Tomb01, ModelType.Tomb03)]
    public class TombObject : ModelObject
    {
        public TombObject()
        {
            LightEnabled = false;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.Tomb01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Tomb{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
