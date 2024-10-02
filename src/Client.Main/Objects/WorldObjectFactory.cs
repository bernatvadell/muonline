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
            foreach (var type in typeof(ModelObject).Assembly.GetTypes())
            {
                var attribute = (ModelObjectTypeAttribute)Attribute.GetCustomAttribute(type, typeof(ModelObjectTypeAttribute));

                if (attribute != null)
                {
                    foreach (var objType in attribute.Types)
                        _modelTypes.Add(objType, type);
                }
            }
        }

        public static ModelObject CreateModelObject(WorldControl world, ModelType type)
        {
            if (!_modelTypes.TryGetValue(type, out Type worldObjectType))
                throw new ArgumentException($"Invalid object type: {type}");

            var modelObject = (ModelObject)Activator.CreateInstance(worldObjectType);

            modelObject.World = world;
            modelObject.Type = (ushort)type;

            return modelObject;
        }

        public static WorldObject CreateMapObject(WorldControl world, Data.OBJS.MapObject obj)
        {
            var type = world.MapTileObjects[obj.Type];

            if (type == null)
                return null;

            WorldObject mapObj = (WorldObject)Activator.CreateInstance(type);

            mapObj.World = world;
            mapObj.Type = obj.Type;
            mapObj.Position = obj.Position;
            mapObj.Scale = obj.Scale;
            mapObj.Angle = obj.Angle;

            return mapObj;
        }
    }
}
