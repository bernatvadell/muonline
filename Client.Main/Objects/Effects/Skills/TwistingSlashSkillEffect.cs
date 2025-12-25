#nullable enable
using Client.Main.Core.Utilities;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Twisting Slash skill visual effect (Skill ID 41).
    /// </summary>
    [SkillVisualEffect(41)]
    public sealed class TwistingSlashSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            return new TwistingSlashEffect(context.Caster);
        }
    }
}
