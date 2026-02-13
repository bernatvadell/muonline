#nullable enable
using Client.Main.Core.Utilities;
using Client.Main.Objects;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Poison skill visual effect (Skill ID 1 - Scroll of Poison).
    /// </summary>
    [SkillVisualEffect(1)]
    public sealed class PoisonSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            WalkerObject? targetWalker = null;
            Vector3 center;
            if (context.TargetId != 0 && context.World.TryGetWalkerById(context.TargetId, out var target))
            {
                targetWalker = target;
                center = target.WorldPosition.Translation + (Vector3.UnitZ * 65f);
            }
            else
            {
                center = context.Caster.WorldPosition.Translation + (Vector3.UnitZ * 65f);
            }

            (targetWalker ?? context.Caster).ApplyTemporaryDebuffTint(effectId: 55, durationSeconds: 3.8f);

            return new ScrollOfPoisonEffect(center);
        }
    }
}
