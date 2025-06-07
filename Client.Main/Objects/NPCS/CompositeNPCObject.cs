using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Objects.NPCS
{
    /// <summary>
    /// Base class for humanoid NPCs composed of multiple parts (head, armor, etc.),
    /// inheriting from HumanoidObject. Implements the clickable NPC pattern.
    /// </summary>
    public abstract class CompositeNPCObject : HumanoidObject
    {
        protected CompositeNPCObject()
        {
            Interactive = true;
        }

        public override void OnClick() => HandleClick();

        // Force derived classes to implement their click logic.
        protected abstract void HandleClick();
    }
}
