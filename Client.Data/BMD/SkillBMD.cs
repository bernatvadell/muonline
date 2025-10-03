namespace Client.Data.BMD
{
    /// <summary>
    /// Skill types based on usage pattern (Area/Target/Self).
    /// </summary>
    public enum SkillType : byte
    {
        Target = 0,  // Requires target selection
        Area = 1,    // Area effect / directional
        Self = 2     // Self-cast / buff
    }

    /// <summary>
    /// Skill use type classification.
    /// </summary>
    public enum SkillUseType : byte
    {
        None = 0,
        Master = 1,
        Brand = 2,
        MasterLevel = 3,
        MasterActive = 4
    }

    /// <summary>
    /// Type of skill effect.
    /// </summary>
    public enum TypeSkill : int
    {
        None = -1,
        CommonAttack = 0,
        Buff = 1,
        DeBuff = 2,
        FriendlySkill = 3
    }

    /// <summary>
    /// Represents a skill definition loaded from <c>skill*.bmd</c> files.
    /// Field layout matches the original MU client <c>SKILL_ATTRIBUTE</c>.
    /// </summary>
    public sealed class SkillBMD
    {
        /// <summary>Maximum number of duty class requirements stored per skill.</summary>
        public const int MaxDutyClass = 3;

        /// <summary>Maximum number of class requirements stored per skill.</summary>
        public const int MaxClass = 7;

        /// <summary>Skill name (UTF-8 in file, exposed as UTF-16 string).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Required character level.</summary>
        public ushort RequiredLevel { get; set; }

        /// <summary>Base damage value.</summary>
        public ushort Damage { get; set; }

        /// <summary>Mana cost per cast.</summary>
        public ushort ManaCost { get; set; }

        /// <summary>Ability Gauge (AG) cost per cast.</summary>
        public ushort AbilityGaugeCost { get; set; }

        /// <summary>Effective range of the skill.</summary>
        public uint Distance { get; set; }

        /// <summary>Cooldown delay in milliseconds.</summary>
        public int Delay { get; set; }

        /// <summary>Required Energy stat.</summary>
        public int RequiredEnergy { get; set; }

        /// <summary>Required Command/Leadership stat.</summary>
        public ushort RequiredLeadership { get; set; }

        /// <summary>Mastery group (original client terminology).</summary>
        public byte MasteryType { get; set; }

        /// <summary>Skill use type (normal, master, brand, etc.).</summary>
        public byte SkillUseType { get; set; }

        /// <summary>Brand identifier (DWORD in the original client).</summary>
        public uint SkillBrand { get; set; }

        /// <summary>Required kill count.</summary>
        public byte KillCount { get; set; }

        /// <summary>Per-duty class requirements (length = <see cref="MaxDutyClass"/>).</summary>
        public byte[] RequireDutyClass { get; } = new byte[MaxDutyClass];

        /// <summary>Per-class requirements (length = <see cref="MaxClass"/>).</summary>
        public byte[] RequireClass { get; } = new byte[MaxClass];

        /// <summary>Skill rank value from the client data.</summary>
        public byte SkillRank { get; set; }

        /// <summary>Icon index used in UI.</summary>
        public ushort MagicIcon { get; set; }

        /// <summary>High-level skill classification used by the client.</summary>
        public TypeSkill Type { get; set; } = TypeSkill.None;

        /// <summary>Required Strength stat.</summary>
        public int RequiredStrength { get; set; }

        /// <summary>Required Dexterity stat.</summary>
        public int RequiredDexterity { get; set; }

        /// <summary>Item skill flag (0 = normal skill).</summary>
        public byte ItemSkill { get; set; }

        /// <summary>True if the skill deals damage.</summary>
        public bool IsDamage { get; set; }

        /// <summary>Effect ID used by the client (see original resources).</summary>
        public ushort Effect { get; set; }

    }
}
