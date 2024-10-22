using Client.Main.Controls;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;

namespace Client.Main
{
    public static class WorldObjectFactory
    {
        public static WorldObject CreateMapTileObject(this WorldControl world, Data.OBJS.IMapObject obj)
        {
            var objType = obj.Type;

            if (objType < 0 || objType >= world.MapTileObjects.Length)
            {
                Debug.WriteLine($"Object {objType} not registered as map tile objects / Pos -> {(int)(obj.Position.X / Constants.TERRAIN_SCALE)}:{(int)(obj.Position.Y / Constants.TERRAIN_SCALE)}");
                return null;
            }

            var type = world.MapTileObjects[objType];

            if (type == null)
            {
                Debug.WriteLine($"Object {objType} not registered as map tile objects / Pos -> {(int)(obj.Position.X / Constants.TERRAIN_SCALE)}:{(int)(obj.Position.Y / Constants.TERRAIN_SCALE)}");
                return null;
            }

            var mapObj = CreateObject(world, type, null);

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

        public static T CreateObject<T>(this WorldControl world, WorldObject parent) where T : WorldObject, new()
        {
            return (T)CreateObject(world, typeof(T), parent);
        }

        public static WorldObject CreateObject(this WorldControl world, Type objectType, WorldObject parent)
        {
            var obj = (WorldObject)Activator.CreateInstance(objectType);
            obj.World = world;
            obj.Parent = parent;

            if (parent == null)
                world.Objects.Add(obj);
            else
                parent.Children.Add(obj);

            return obj;
        }

        public static void AddObject(this WorldControl world, WorldObject obj)
        {
            obj.World = world;
            world.Objects.Add(obj);
        }
    }
}
