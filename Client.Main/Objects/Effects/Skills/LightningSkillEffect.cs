#nullable enable
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Lightning skill visual effect (Skill ID 3 - Scroll of Lightning).
    /// Creates a ScrollOfLightningEffect between caster and target.
    /// </summary>
    [SkillVisualEffect(3)] // Lightning / Scroll of Lightning
    public sealed class LightningSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var caster = context.Caster;
            var world = context.World;
            ushort targetId = context.TargetId;

            // Capture caster's hand position at spawn time (locked source)
            Vector3 lockedSource;
            if (caster is PlayerObject player && player.TryGetHandWorldMatrix(isLeftHand: false, out var handMatrix))
            {
                lockedSource = handMatrix.Translation;
            }
            else
            {
                lockedSource = caster.WorldPosition.Translation + Vector3.UnitZ * 80f;
            }

            // Source provider returns locked position
            Vector3 SourceProvider() => lockedSource;

            // Target provider follows the target (or falls back to caster position)
            Vector3 TargetProvider()
            {
                if (targetId != 0 && world.TryGetWalkerById(targetId, out var target))
                    return target.WorldPosition.Translation + Vector3.UnitZ * 80f;

                return caster.WorldPosition.Translation + Vector3.UnitZ * 80f;
            }

            // Note: Don't call Load() here - caller adds to world first, then loads
            return new ScrollOfLightningEffect(SourceProvider, TargetProvider);
        }
    }
}
