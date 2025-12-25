using Client.Main.Controls;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Context data passed to skill visual effects when spawning.
    /// </summary>
    public readonly struct SkillEffectContext
    {
        /// <summary>
        /// The caster (player/monster) who used the skill.
        /// </summary>
        public WalkerObject Caster { get; init; }

        /// <summary>
        /// The target ID (0 if no specific target or area skill).
        /// </summary>
        public ushort TargetId { get; init; }

        /// <summary>
        /// The skill ID being cast.
        /// </summary>
        public ushort SkillId { get; init; }

        /// <summary>
        /// The world control where the effect will be spawned.
        /// </summary>
        public WalkableWorldControl World { get; init; }
    }
}
