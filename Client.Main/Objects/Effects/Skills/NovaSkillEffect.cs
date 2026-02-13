#nullable enable
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Nova release/explosion visual (Skill ID 40).
    /// </summary>
    [Core.Utilities.SkillVisualEffect(40)]
    public sealed class NovaSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            byte stage = ScrollOfNovaChargeEffect.ConsumeStageAndStop(context.World, context.Caster.NetworkId);
            Vector3 center = context.Caster.WorldPosition.Translation;
            return new ScrollOfNovaExplosionEffect(context.Caster, center, stage);
        }
    }
}
