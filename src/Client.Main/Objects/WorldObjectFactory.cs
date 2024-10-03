using Client.Data;
using Client.Main.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class WorldObjectFactory
    {
        private static readonly Dictionary<ModelType, Type> _modelTypes = [];

        static WorldObjectFactory()
        {
            foreach (var type in typeof(WorldObject).Assembly.GetTypes())
            {
                var attribute = (MapObjectTypeAttribute)Attribute.GetCustomAttribute(type, typeof(MapObjectTypeAttribute));

                if (attribute != null)
                {
                    foreach (var objType in attribute.Types)
                        _modelTypes.Add(objType, type);
                }
            }
        }

        public static WorldObject CreateMapObject(WorldControl world, ModelType type)
        {
            if (!_modelTypes.TryGetValue(type, out Type worldObjectType))
                throw new ArgumentException($"Invalid object type: {type}");

            var modelObject = (WorldObject)Activator.CreateInstance(worldObjectType);

            modelObject.World = world;
            modelObject.Type = (ushort)type;

            return modelObject;
        }

        public static WorldObject CreateMapTileObject(WorldControl world, Data.OBJS.MapObject obj)
        {
            var type = world.MapTileObjects[obj.Type];

            if (type == null)
                return null;

            var mapObj = (WorldObject)Activator.CreateInstance(type);

            mapObj.World = world;
            mapObj.Type = obj.Type;
            mapObj.Position = obj.Position;
            mapObj.Scale = obj.Scale;
            mapObj.Angle = obj.Angle;

            return mapObj;
        }
    }
}
