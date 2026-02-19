using Client.Main.Controls;
using Client.Main.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Client.Main
{
    public static class WorldObjectFactory
    {
        private static readonly ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger(typeof(WorldObjectFactory));
        private static readonly ConcurrentDictionary<Type, Func<WorldObject>> _ctorCache = new();

        public static WorldObject CreateMapTileObject(this WorldControl world, Client.Data.OBJS.IMapObject obj)
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
            mapObj.IsMapPlacementObject = true;
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
            var obj = CreateInstance(objectType);
            world.Objects.Add(obj);
            return obj;
        }

        public static WorldObject CreateObject(this WorldObject parent, Type objectType)
        {
            ArgumentNullException.ThrowIfNull(parent);
            var obj = CreateInstance(objectType);
            parent.Children.Add(obj);
            return obj;
        }

        private static WorldObject CreateInstance(Type objectType)
        {
            // Cache parameterless ctor delegate to avoid reflection overhead on every spawn
            var ctor = _ctorCache.GetOrAdd(objectType, static t =>
            {
                var newExpr = Expression.New(t);
                var castExpr = Expression.Convert(newExpr, typeof(WorldObject));
                return Expression.Lambda<Func<WorldObject>>(castExpr).Compile();
            });
            return ctor();
        }
    }
}
