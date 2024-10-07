using Client.Data.OBJS;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class MapTileObject : ModelObject
    {
        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var modelPath = Path.Join($"Object{World.WorldIndex}", $"Object{(Type + 1).ToString().PadLeft(2, '0')}.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
                Debug.WriteLine($"Can't load MapObject for model: {modelPath}");

            await base.Load(graphicsDevice);
        }
    }
}
