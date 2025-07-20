namespace Client.Main.Models
{
    /// <summary>
    /// Data structure for equipment slot information from AppearanceChanged packet
    /// </summary>
    public class EquipmentSlotData
    {
        /// <summary>
        /// Item group (0-15) - actual group after decoding
        /// </summary>
        public byte ItemGroup { get; set; }

        /// <summary>
        /// Item number within the group
        /// </summary>
        public ushort ItemNumber { get; set; }

        /// <summary>
        /// Final calculated item type (group * 512 + number)
        /// </summary>
        public int ItemType { get; set; }

        /// <summary>
        /// Item level (0-15)
        /// </summary>
        public byte ItemLevel { get; set; }

        /// <summary>
        /// Item options (Skill, Luck, etc.)
        /// </summary>
        public byte ItemOptions { get; set; }

        /// <summary>
        /// Excellent flags bitmask
        /// </summary>
        public byte ExcellentFlags { get; set; }

        /// <summary>
        /// Ancient set discriminator
        /// </summary>
        public byte AncientDiscriminator { get; set; }

        /// <summary>
        /// Whether the character has a complete ancient set
        /// </summary>
        public bool IsAncientSetComplete { get; set; }
    }
}