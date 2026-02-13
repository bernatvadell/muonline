#nullable enable
using Client.Main.Core.Utilities;
using Client.Main.Objects;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Ice skill visual effect (Skill ID 7 - Scroll of Ice / Slow).
    /// </summary>
    [SkillVisualEffect(7)]
    public sealed class IceSkillEffect : ISkillVisualEffect
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
                center = target.WorldPosition.Translation + (Vector3.UnitZ * 70f);
            }
            else
            {
                center = context.Caster.WorldPosition.Translation + (Vector3.UnitZ * 70f);
            }

            (targetWalker ?? context.Caster).ApplyTemporaryDebuffTint(effectId: 56, durationSeconds: 3.2f);

            return new ScrollOfIceEffect(center);
        }
    }
}
