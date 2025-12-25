using System;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Marks a class as a visual effect handler for a specific skill ID.
    /// The class must implement <see cref="Client.Main.Objects.Effects.Skills.ISkillVisualEffect"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class SkillVisualEffectAttribute : Attribute
    {
        /// <summary>
        /// Gets the skill ID that this effect handles.
        /// </summary>
        public ushort SkillId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillVisualEffectAttribute"/> class.
        /// </summary>
        /// <param name="skillId">The skill ID to handle.</param>
        public SkillVisualEffectAttribute(ushort skillId)
        {
            SkillId = skillId;
        }
    }
}
