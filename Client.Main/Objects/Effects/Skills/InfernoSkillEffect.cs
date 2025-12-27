#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Inferno skill visual effect (Skill ID 14).
    /// </summary>
    [SkillVisualEffect(14)]
    public sealed class InfernoSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            Vector3 center = context.Caster.WorldPosition.Translation;
            return new ScrollOfInfernoEffect(context.Caster, center);
        }
    }
}
