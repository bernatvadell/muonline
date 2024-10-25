using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class BeerObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.Beer01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Beer{idx}.bmd");
            await base.Load();
        }
    }

}
