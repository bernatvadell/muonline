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
    [MapObjectType(min: ModelType.StoneStatue01, max: ModelType.StoneStatue03)]
    public class StoneStatueObject : WorldObject
    {
        public StoneStatueObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.StoneStatue01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/StoneStatue{idx}.bmd");
            await base.Load(graphicsDevice);
        }
    }
}
