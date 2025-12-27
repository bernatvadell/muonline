#nullable enable
using Client.Main.Core.Utilities;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Factory for Death Stab (Orb of Blow) skill visual effect (Skill ID 43).
    /// </summary>
    [SkillVisualEffect(43)]
    public sealed class DeathStabSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            WalkerObject? target = null;
            if (context.TargetId != 0)
            {
                context.World.TryGetWalkerById(context.TargetId, out target);
            }

            return new DeathStabEffect(context.Caster, target);
        }
    }
}
