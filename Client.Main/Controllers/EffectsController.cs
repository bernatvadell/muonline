using Client.Main.Objects;
using System.Collections.Generic;

namespace Client.Main.Controllers
{
    public class EffectsController(WorldObject owner)
    {
        public WorldObject Owner { get; } = owner;

        public List<WorldObject> ActiveEffects { get; } = [];
    }
}
