#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Aqua Beam skill visual effect (Skill ID 12).
    /// </summary>
    [SkillVisualEffect(12)]
    public sealed class AquaBeamSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            Vector3? targetPosition = null;
            if (context.TargetId != 0 && context.World.TryGetWalkerById(context.TargetId, out var target))
            {
                targetPosition = target.WorldPosition.Translation;
            }

            return new ScrollOfAquaBeamEffect(context.Caster, targetPosition);
        }
    }
}
