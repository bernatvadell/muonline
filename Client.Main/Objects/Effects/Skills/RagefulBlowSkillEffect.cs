#nullable enable
using Client.Main.Core.Utilities;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Rageful Blow skill visual effect (Skill ID 42).
    /// </summary>
    [SkillVisualEffect(42)]
    public sealed class RagefulBlowSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            return new RagefulBlowEffect(context.Caster, context.TargetPosition);
        }
    }
}
