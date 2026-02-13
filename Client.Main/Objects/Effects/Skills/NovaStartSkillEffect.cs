#nullable enable

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Nova charging visual (Skill ID 58, NovaStart).
    /// </summary>
    [Core.Utilities.SkillVisualEffect(58)]
    public sealed class NovaStartSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            return ScrollOfNovaChargeEffect.GetOrCreate(context.World, context.Caster);
        }
    }
}
