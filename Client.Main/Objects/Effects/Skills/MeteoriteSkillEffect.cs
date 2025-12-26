#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Meteorite skill visual effect (Skill ID 2 - Scroll of Meteorite).
    /// </summary>
    [SkillVisualEffect(2)] // Meteorite / Scroll of Meteorite
    public sealed class MeteoriteSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var world = context.World;
            ushort targetId = context.TargetId;

            Vector3 targetPosition;
            if (targetId != 0 && world.TryGetWalkerById(targetId, out var target))
                targetPosition = target.WorldPosition.Translation;
            else
                targetPosition = context.Caster.WorldPosition.Translation;

            return new ScrollOfMeteoriteEffect(targetPosition);
        }
    }
}
