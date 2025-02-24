using Client.Main.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controllers
{
    public class EffectsController(WorldObject owner)
    {
        public WorldObject Owner { get; } = owner;

        public List<WorldObject> ActiveEffects { get; } = [];
    }
}
