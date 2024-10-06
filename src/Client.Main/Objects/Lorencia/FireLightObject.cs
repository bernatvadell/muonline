using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class FireLightObject : ModelObject
    {
        public FireLightObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.FireLight01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/FireLight{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
