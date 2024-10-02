using Client.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [ModelObjectType(
        ModelType.Unknown48,

        ModelType.Unknown79,

        ModelType.Unknown86,

        ModelType.Unknown94,

        ModelType.Unknown113,

        ModelType.Unknown134,
        ModelType.Unknown135,
        ModelType.Unknown136,
        ModelType.Unknown137,
        ModelType.Unknown138,
        ModelType.Unknown139,
        
        ModelType.Unknown147,

        ModelType.Unknown157,
        ModelType.Unknown159
    )]
    public class UnknownObject : ModelObject
    {
        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Debug.WriteLine($"Unknown Type Model and BMD: {Type}");
        }

    }
}
