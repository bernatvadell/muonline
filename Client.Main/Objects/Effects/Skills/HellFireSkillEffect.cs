#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for HellFire skill visual effect (Skill ID 10).
    /// </summary>
    [SkillVisualEffect(10)]
    public sealed class HellFireSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            Vector3 center = context.Caster.WorldPosition.Translation;
            return new ScrollOfHellFireEffect(context.Caster, center);
        }
    }
}
