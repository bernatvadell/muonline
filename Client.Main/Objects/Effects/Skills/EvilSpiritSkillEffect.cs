#nullable enable
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Evil Spirit skill visual effect (Skill ID 9).
    /// Creates spirit bolts that move in 4 directions (90° intervals) with trailing effects.
    /// </summary>
    [SkillVisualEffect(9)]
    public sealed class EvilSpiritSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            // Calculate target angle from caster to target position
            float targetAngle = 0f;
            Vector3 casterPos = context.Caster.WorldPosition.Translation;

            if (context.TargetPosition.HasValue)
            {
                Vector3 targetPos = context.TargetPosition.Value;
                Vector2 direction = new Vector2(targetPos.X - casterPos.X, targetPos.Y - casterPos.Y);

                if (direction.LengthSquared() > 0.01f)
                {
                    targetAngle = MathHelper.ToDegrees(MathF.Atan2(direction.Y, direction.X));
                }
            }
            else if (context.TargetId != 0)
            {
                // Try to find the target walker by ID
                WalkerObject? target = FindTargetById(context.World, context.TargetId);
                if (target != null)
                {
                    Vector3 targetPos = target.WorldPosition.Translation;
                    Vector2 direction = new Vector2(targetPos.X - casterPos.X, targetPos.Y - casterPos.Y);

                    if (direction.LengthSquared() > 0.01f)
                    {
                        targetAngle = MathHelper.ToDegrees(MathF.Atan2(direction.Y, direction.X));
                    }
                }
            }
            else
            {
                // Use caster's facing direction if no target
                targetAngle = context.Caster.Angle.Z;
            }

            return new ScrollOfEvilSpiritEffect(context.Caster, targetAngle);
        }

        private static WalkerObject? FindTargetById(WalkableWorldControl world, ushort targetId)
        {
            // Check monsters
            foreach (var monster in world.Monsters)
            {
                if (monster?.NetworkId == targetId)
                    return monster;
            }

            // Check players
            foreach (var player in world.Players)
            {
                if (player?.NetworkId == targetId)
                    return player;
            }

            return null;
        }
    }
}
