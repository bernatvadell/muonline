using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    [MapObjectType(min: ModelType.HouseWall01, max: ModelType.HouseWall06)]
    public class HouseWallObject : WorldObject
    {
        public HouseWallObject()
        {
            LightEnabled = true;
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var idx = (Type - (ushort)ModelType.HouseWall01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/HouseWall{idx}.bmd");

            await base.Load(graphicsDevice);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Type == (ushort)ModelType.HouseWall02)
                BlendMeshLight = (MuGame.Random.Next() % 4 + 4) * 0.1f;
        }
    }
}
