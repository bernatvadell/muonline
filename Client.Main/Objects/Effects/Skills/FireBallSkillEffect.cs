#nullable enable
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Fire Ball skill visual effect (Skill ID 4 - Scroll of Fire Ball).
    /// </summary>
    [SkillVisualEffect(4)] // Fire Ball / Scroll of Fire Ball
    public sealed class FireBallSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var caster = context.Caster;
            var world = context.World;
            ushort targetId = context.TargetId;

            Vector3 startPosition;
            if (caster is PlayerObject player && player.TryGetHandWorldMatrix(isLeftHand: false, out var handMatrix))
            {
                startPosition = handMatrix.Translation + new Vector3(0f, 0f, 20f);
            }
            else
            {
                startPosition = caster.WorldPosition.Translation + Vector3.UnitZ * 80f;
            }

            Vector3 targetPosition;
            if (targetId != 0 && world.TryGetWalkerById(targetId, out var target))
            {
                targetPosition = target.WorldPosition.Translation + Vector3.UnitZ * 80f;
            }
            else
            {
                targetPosition = caster.WorldPosition.Translation + Vector3.UnitZ * 80f;
            }

            return new ScrollOfFireBallEffect(startPosition, targetPosition);
        }
    }
}
