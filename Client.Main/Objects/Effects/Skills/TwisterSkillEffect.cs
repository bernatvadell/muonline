#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Twister skill visual effect (Skill ID 8).
    /// </summary>
    [SkillVisualEffect(8)]
    public sealed class TwisterSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            Vector3 center = context.Caster.WorldPosition.Translation;
            Vector3? targetPosition = context.TargetPosition;
            if (targetPosition == null && context.TargetId != 0 && context.World.TryGetWalkerById(context.TargetId, out var target))
            {
                targetPosition = target.WorldPosition.Translation;
            }

            return new ScrollOfTwisterEffect(context.Caster, center, targetPosition);
        }
    }
}
