#nullable enable
namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Interface for skill visual effect factories.
    /// Classes implementing this interface and decorated with
    /// <see cref="Core.Utilities.SkillVisualEffectAttribute"/> will be
    /// automatically discovered and registered.
    /// </summary>
    public interface ISkillVisualEffect
    {
        /// <summary>
        /// Creates the visual effect for the skill.
        /// </summary>
        /// <param name="context">Context containing caster, target, and world information.</param>
        /// <returns>A WorldObject representing the effect, or null if effect cannot be created.</returns>
        WorldObject? CreateEffect(SkillEffectContext context);
    }
}
