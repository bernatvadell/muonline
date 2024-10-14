using Client.Data;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public static class WorldObjectFactory
    {
        public static WorldObject CreateMapTileObject(this WorldControl world, Data.OBJS.IMapObject obj)
        {
            var type = world.MapTileObjects[obj.Type];

            if (type == null)
            {
                Debug.WriteLine($"Object {obj.Type} not registered as map tile objects");
                return null;
            }

            var mapObj = (WorldObject)Activator.CreateInstance(type);

            mapObj.Type = obj.Type;
            mapObj.Position = obj.Position;
            mapObj.Scale = obj.Scale;
            mapObj.Angle = new Vector3(
               MathHelper.ToRadians(obj.Angle.X),
               MathHelper.ToRadians(obj.Angle.Y),
               MathHelper.ToRadians(obj.Angle.Z)
            );
            return mapObj;
        }
    }
}
