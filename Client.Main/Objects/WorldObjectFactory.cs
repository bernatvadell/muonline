using Client.Main.Controls;
using Client.Main.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System;

namespace Client.Main
{
    public static class WorldObjectFactory
    {
        private static readonly ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger(typeof(WorldObjectFactory));

        public static WorldObject CreateMapTileObject(this WorldControl world, Data.OBJS.IMapObject obj)
        {
            var objType = obj.Type;

            if (objType < 0 || objType >= world.MapTileObjects.Length)
            {
                _logger?.LogDebug($"Object {objType} not registered as map tile objects / Pos -> {(int)(obj.Position.X / Constants.TERRAIN_SCALE)}:{(int)(obj.Position.Y / Constants.TERRAIN_SCALE)}");
                return null;
            }

            var type = world.MapTileObjects[objType];

            if (type == null)
            {
                _logger?.LogDebug($"Object {objType} not registered as map tile objects / Pos -> {(int)(obj.Position.X / Constants.TERRAIN_SCALE)}:{(int)(obj.Position.Y / Constants.TERRAIN_SCALE)}");
                return null;
            }

            var mapObj = CreateObject(world, type);

            mapObj.Type = objType;
            mapObj.Position = obj.Position;
            mapObj.Scale = obj.Scale;
            mapObj.Angle = new Vector3(
               MathHelper.ToRadians(obj.Angle.X),
               MathHelper.ToRadians(obj.Angle.Y),
               MathHelper.ToRadians(obj.Angle.Z)
            );
            return mapObj;
        }

        public static WorldObject CreateObject(this WorldControl world, Type objectType)
        {
            ArgumentNullException.ThrowIfNull(world);
            var obj = (WorldObject)Activator.CreateInstance(objectType);
            world.Objects.Add(obj);
            return obj;
        }

        public static WorldObject CreateObject(this WorldObject parent, Type objectType)
        {
            ArgumentNullException.ThrowIfNull(parent);
            var obj = (WorldObject)Activator.CreateInstance(objectType);
            parent.Children.Add(obj);
            return obj;
        }
    }
}
