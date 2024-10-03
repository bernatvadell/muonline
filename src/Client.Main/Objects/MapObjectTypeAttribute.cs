using Client.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class MapObjectTypeAttribute : Attribute
    {
        public ModelType[] Types { get; set; }

        public MapObjectTypeAttribute(params ModelType[] types)
        {
            Types = types;
        }

        public MapObjectTypeAttribute(ModelType min, ModelType max)
        {
            var umin = (ushort)min;
            var umax = (ushort)max;

            var types = new ModelType[umax - umin + 1];

            for (var i = umin; i <= umax; i++)
                types[i - umin] = (ModelType)i;

            Types = types;
        }
    }
}
