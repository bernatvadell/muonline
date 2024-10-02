using Client.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class ModelObjectTypeAttribute : Attribute
    {
        public ModelType[] Types { get; set; }

        public ModelObjectTypeAttribute(params ModelType[] types)
        {
            Types = types;
        }

        public ModelObjectTypeAttribute(ModelType min, ModelType max)
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
