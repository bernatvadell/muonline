#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Flame skill visual effect (Skill ID 5 - Scroll of Flame).
    /// </summary>
    [SkillVisualEffect(5)] // Flame / Scroll of Flame
    public sealed class FlameSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var world = context.World;
            Vector3 center;

            if (context.TargetPosition.HasValue)
            {
                center = context.TargetPosition.Value;
            }
            else if (context.TargetId != 0 && world.TryGetWalkerById(context.TargetId, out var target))
            {
                center = target.WorldPosition.Translation;
            }
            else
            {
                center = context.Caster.WorldPosition.Translation;
            }

            if (world.Terrain != null)
            {
                float groundZ = world.Terrain.RequestTerrainHeight(center.X, center.Y);
                center = new Vector3(center.X, center.Y, groundZ);
            }

            bool isTargeted = context.TargetId != 0;
            bool dealsDamage = context.Caster.IsMainWalker;
            return new ScrollOfFlameEffect(center, isTargeted, dealsDamage);
        }
    }
}
